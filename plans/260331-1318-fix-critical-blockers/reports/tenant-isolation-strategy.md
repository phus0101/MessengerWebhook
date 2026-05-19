# Tenant Isolation Strategy

## Overview

Multi-tenant architecture với global query filters để đảm bảo data isolation giữa các tenants. Mỗi tenant được identify bởi `TenantId` (Guid) và `FacebookPageId` (string).

## Architecture

### ITenantOwnedEntity Interface

```csharp
public interface ITenantOwnedEntity
{
    Guid? TenantId { get; set; }
}
```

Tất cả entities cần tenant isolation implement interface này.

### Global Query Filters

**Location:** `MessengerBotDbContext.ApplyTenantFilters()` (lines 365-435)

**Filter Pattern:**
```csharp
modelBuilder.Entity<EntityType>()
    .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);
```

**Logic:**
- `!IsTenantResolved`: Nếu tenant context chưa resolved → return all data (admin mode)
- `x.TenantId == null`: Shared/global data visible cho tất cả tenants
- `x.TenantId == CurrentTenantId`: Data thuộc tenant hiện tại

### Protected Entities (15 types)

1. **Product** - Sản phẩm của tenant
2. **ProductVariant** - Biến thể sản phẩm (via Product.TenantId)
3. **ProductGiftMapping** - Mapping quà tặng (via Product.TenantId)
4. **Gift** - Quà tặng của tenant
5. **ConversationSession** - Phiên chat của tenant
6. **ConversationMessage** - Tin nhắn (via Session.TenantId)
7. **Cart** - Giỏ hàng (via Session.TenantId)
8. **CartItem** - Item trong giỏ (via Cart.Session.TenantId)
9. **Order** - Đơn hàng (via Session.TenantId)
10. **OrderItem** - Item trong đơn (via Order.Session.TenantId)
11. **CustomerIdentity** - Thông tin khách hàng
12. **DraftOrder** - Draft order chờ xử lý
13. **DraftOrderItem** - Item trong draft (via DraftOrder.TenantId)
14. **RiskSignal** - Tín hiệu rủi ro khách hàng
15. **VipProfile** - Profile VIP khách hàng
16. **HumanSupportCase** - Case support thủ công
17. **BotConversationLock** - Lock bot conversation
18. **KnowledgeSnapshot** - Snapshot kiến thức sản phẩm
19. **AdminAuditLog** - Audit log admin actions

## Tenant Context Resolution

### ITenantContext Service

**Implementation:** `TenantContext.cs`

```csharp
public interface ITenantContext
{
    Guid? TenantId { get; }
    string? FacebookPageId { get; }
    string? ManagerEmail { get; }
    bool IsResolved { get; }
    void Initialize(Guid? tenantId, string? facebookPageId, string? managerEmail);
    void Clear();
}
```

### Middleware

**TenantResolutionMiddleware** - Resolve tenant từ Facebook webhook events
**AdminTenantContextMiddleware** - Resolve tenant từ JWT claims cho admin API

## Service Layer Patterns

### Pattern 1: Rely on Global Filters (Recommended)

```csharp
// CustomerIntelligenceService.cs
return await _dbContext.CustomerIdentities
    .Where(x => x.FacebookPSID == facebookPsid)
    .FirstOrDefaultAsync(cancellationToken);
```

Global filters tự động apply `TenantId == CurrentTenantId`.

### Pattern 2: Explicit TenantId Filter (Admin Services)

```csharp
// AdminDashboardQueryService.cs
return await _dbContext.Products
    .Where(x => x.TenantId == user.TenantId)
    .ToListAsync();
```

Dùng khi cần explicit control hoặc trong admin context.

### Pattern 3: Bypass Filters (Admin Only)

```csharp
// AdminAuthService.cs - Admin operations
var allTenants = await _dbContext.Tenants
    .IgnoreQueryFilters()
    .ToListAsync();
```

⚠️ **Chỉ dùng trong admin context với audit logging!**

## Testing Strategy

### Integration Tests

**File:** `tests/MessengerWebhook.IntegrationTests/TenantIsolationTests.cs`

**Coverage:** 6 tests covering:
- Products isolation
- CustomerIdentities isolation
- DraftOrders isolation
- ConversationSessions isolation
- VipProfiles isolation
- RiskSignals isolation

**Test Pattern:**
1. Create entities cho 2 tenants khác nhau (no tenant context)
2. Query với tenant 1 context → verify chỉ thấy data của tenant 1
3. Query với tenant 2 context → verify chỉ thấy data của tenant 2

**Results:** ✅ 6/6 tests passing

## Security Considerations

### Strengths

✅ Global query filters prevent accidental cross-tenant queries
✅ Filters apply automatically - không cần remember thêm filter vào mỗi query
✅ Null TenantId cho shared data (products, gifts) visible cho all tenants
✅ Admin services có explicit filters với audit logging

### Potential Risks (From Code Review)

⚠️ **Child Entity Coverage** - DraftOrderItem, ConversationMessage, ProductVariant chưa có direct tests (rely on parent filters)
⚠️ **IgnoreQueryFilters() Authorization** - 23 uses trong codebase, cần verify authorization
⚠️ **Unresolved Context Behavior** - `!IsTenantResolved` returns all data (fail-open approach)
⚠️ **Eager Loading** - `.Include()` behavior với tenant filters chưa được test

### Recommendations

**Before Production:**
1. Add child entity isolation tests
2. Add IgnoreQueryFilters authorization tests
3. Add null TenantId behavior tests
4. Add unresolved context behavior tests
5. Add eager loading tests

**Performance:**
- Verify indexes on TenantId columns
- Monitor query performance với filters

## Best Practices

### DO ✅

- Implement ITenantOwnedEntity cho entities cần isolation
- Set TenantId khi create new entities: `TenantId = _tenantContext.TenantId`
- Rely on global filters cho normal queries
- Use explicit filters trong admin services với audit logging
- Clear change tracker trước khi switch tenant context trong tests

### DON'T ❌

- Bypass filters với IgnoreQueryFilters() ngoài admin context
- Forget set TenantId khi create entities
- Assume tenant context is always resolved
- Use hard-coded TenantId values trong queries

## Maintenance

### Adding New Tenant-Owned Entity

1. Implement `ITenantOwnedEntity` interface
2. Add global query filter trong `MessengerBotDbContext.ApplyTenantFilters()`
3. Set `TenantId = _tenantContext.TenantId` khi create
4. Add integration test trong `TenantIsolationTests.cs`
5. Verify indexes on TenantId column

### Debugging Tenant Issues

1. Check `IsTenantResolved` - tenant context có được resolve không?
2. Check `CurrentTenantId` - đúng tenant ID không?
3. Use `.IgnoreQueryFilters()` temporarily để see all data
4. Check entity có implement `ITenantOwnedEntity` không?
5. Verify global filter được apply trong `OnModelCreating`

## References

- Plan: `plans/260331-1318-fix-critical-blockers/plan.md`
- Code Review: `plans/260331-1318-fix-critical-blockers/reports/code-reviewer-260331-1401-phase2-phase3-review.md`
- Tests: `tests/MessengerWebhook.IntegrationTests/TenantIsolationTests.cs`
- DbContext: `src/MessengerWebhook/Data/MessengerBotDbContext.cs` (lines 365-435)
