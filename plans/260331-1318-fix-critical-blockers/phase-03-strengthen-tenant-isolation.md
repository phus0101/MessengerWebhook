# Phase 3: Strengthen Tenant Data Isolation

## Context

Multi-tenant architecture relies on FacebookPageId uniqueness for data isolation. If page IDs reused or misconfigured, cross-tenant data leaks possible. Need explicit TenantId filters on all ITenantOwnedEntity queries.

## Priority

**P1 - Critical Security Risk**

## Current Status

✅ Completed (2026-03-31)

## Overview

Audit and strengthen tenant data isolation by verifying global query filters in DbContext cover all ITenantOwnedEntity types. Audit services for explicit TenantId filters and ensure no queries bypass filters without audit logging.

## Key Insights

- MessengerBotDbContext already has ApplyTenantFilters (lines 365-435)
- 15 entity types implement ITenantOwnedEntity
- Global query filters already applied to most entities
- Some services may have explicit FacebookPageId filters instead of relying on TenantId
- Risk: If FacebookPageId reused across tenants, data leaks possible

## Requirements

### Functional
- All ITenantOwnedEntity types have global query filters
- No queries bypass filters without explicit IgnoreQueryFilters() + audit log
- Services use TenantId for filtering, not FacebookPageId
- Integration tests verify cross-tenant isolation

### Non-Functional
- No breaking changes to existing queries
- Performance impact minimal (TenantId already indexed)
- Documentation updated with tenant isolation patterns
- Audit trail for any IgnoreQueryFilters() usage

## Architecture

### Current Query Filter Implementation

```csharp
private void ApplyTenantFilters(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>()
        .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

    // ... 14 more entity types
}
```

### Tenant Resolution Flow

```
HTTP Request
  → TenantResolutionMiddleware extracts FacebookPageId from header/route
  → Looks up FacebookPageConfig by FacebookPageId
  → Sets TenantContext.TenantId
  → DbContext.CurrentTenantId returns TenantContext.TenantId
  → Query filters automatically apply TenantId filter
```

### Entities Implementing ITenantOwnedEntity

From grep results (16 files):
1. Product
2. Gift
3. ProductGiftMapping
4. ConversationSession
5. ConversationMessage
6. FacebookPageConfig
7. ManagerProfile
8. CustomerIdentity
9. DraftOrder
10. RiskSignal
11. VipProfile
12. HumanSupportCase
13. BotConversationLock
14. KnowledgeSnapshot
15. AdminAuditLog

## Related Code Files

### To Audit
- `src/MessengerWebhook/Data/MessengerBotDbContext.cs` (lines 365-435) - Query filters
- `src/MessengerWebhook/Services/Customers/CustomerIntelligenceService.cs` - Audit queries
- `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs` - Audit queries
- `src/MessengerWebhook/Data/Repositories/SessionRepository.cs` - Audit queries
- `src/MessengerWebhook/Services/Admin/AdminDashboardQueryService.cs` - Check IgnoreQueryFilters usage
- `src/MessengerWebhook/Services/Admin/AdminAuthService.cs` - Check IgnoreQueryFilters usage

### To Create
- `tests/MessengerWebhook.IntegrationTests/TenantIsolationTests.cs` - Integration tests
- `docs/tenant-isolation-strategy.md` - Documentation

## Implementation Steps

### Step 1: Audit Query Filters Completeness (30min)

**File:** `src/MessengerWebhook/Data/MessengerBotDbContext.cs`

Verify all 15 ITenantOwnedEntity types have query filters:

```bash
# Check which entities implement ITenantOwnedEntity
grep -r "ITenantOwnedEntity" src/MessengerWebhook/Data/Entities/

# Check which entities have query filters
grep -A 1 "HasQueryFilter" src/MessengerWebhook/Data/MessengerBotDbContext.cs
```

Create checklist:
- [ ] Product - Has filter (line 367)
- [ ] Gift - Has filter (line 376)
- [ ] ProductGiftMapping - Has filter (line 379)
- [ ] ConversationSession - Has filter (line 382)
- [ ] ConversationMessage - Has filter (line 397)
- [ ] FacebookPageConfig - Has filter (line 403)
- [ ] ManagerProfile - Has filter (line 406)
- [ ] CustomerIdentity - Has filter (line 409)
- [ ] DraftOrder - Has filter (line 412)
- [ ] RiskSignal - Has filter (line 418)
- [ ] VipProfile - Has filter (line 421)
- [ ] HumanSupportCase - Has filter (line 424)
- [ ] BotConversationLock - Has filter (line 427)
- [ ] KnowledgeSnapshot - Has filter (line 430)
- [ ] AdminAuditLog - Has filter (line 433)

### Step 2: Audit Service Queries (1h)

**Files to audit:**
- CustomerIntelligenceService.cs
- DraftOrderService.cs
- SessionRepository.cs
- AdminDashboardQueryService.cs
- AdminAuthService.cs

For each service, check:
1. Does it query ITenantOwnedEntity types?
2. Does it rely on global query filters or explicit TenantId filters?
3. Does it use IgnoreQueryFilters()? If yes, is it logged?
4. Does it filter by FacebookPageId instead of TenantId?

Create audit report:

```markdown
## Service Audit Results

### CustomerIntelligenceService
- Queries: CustomerIdentity, DraftOrder, RiskSignal
- Filter strategy: [Global filters / Explicit TenantId / FacebookPageId]
- IgnoreQueryFilters usage: [Yes/No]
- Issues found: [List]

### DraftOrderService
- Queries: DraftOrder, DraftOrderItem
- Filter strategy: [Global filters / Explicit TenantId / FacebookPageId]
- IgnoreQueryFilters usage: [Yes/No]
- Issues found: [List]

### SessionRepository
- Queries: ConversationSession
- Filter strategy: [Global filters / Explicit TenantId / FacebookPageId]
- IgnoreQueryFilters usage: [Yes/No]
- Issues found: [List]
```

### Step 3: Add Missing Query Filters (30min)

If any ITenantOwnedEntity types missing query filters, add them:

```csharp
modelBuilder.Entity<MissingEntity>()
    .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);
```

### Step 4: Fix Service Query Issues (1h)

For services using FacebookPageId instead of TenantId:

**Before:**
```csharp
var orders = await _dbContext.DraftOrders
    .Where(x => x.FacebookPageId == pageId)
    .ToListAsync();
```

**After:**
```csharp
// Rely on global query filter (TenantId already set by middleware)
var orders = await _dbContext.DraftOrders
    .ToListAsync();
```

For services using IgnoreQueryFilters without audit:

**Before:**
```csharp
var allOrders = await _dbContext.DraftOrders
    .IgnoreQueryFilters()
    .ToListAsync();
```

**After:**
```csharp
var allOrders = await _dbContext.DraftOrders
    .IgnoreQueryFilters()
    .ToListAsync();

await _auditService.LogAdminActionAsync(
    "CrossTenantQuery",
    "DraftOrder",
    null,
    $"Admin {_adminUserContext.Email} queried all tenants");
```

### Step 5: Write Integration Tests (1h)

**File:** `tests/MessengerWebhook.IntegrationTests/TenantIsolationTests.cs`

```csharp
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MessengerWebhook.IntegrationTests;

public class TenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TenantIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Products_FilteredByTenantId()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        // Seed data for two tenants
        dbContext.Products.Add(new Product { Code = "PROD_A", Name = "Product A", TenantId = tenantA });
        dbContext.Products.Add(new Product { Code = "PROD_B", Name = "Product B", TenantId = tenantB });
        await dbContext.SaveChangesAsync();

        // Act - Query as Tenant A
        var tenantContext = new TenantContext { TenantId = tenantA, IsResolved = true };
        var dbContextA = new MessengerBotDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<MessengerBotDbContext>>(),
            tenantContext);

        var productsA = await dbContextA.Products.ToListAsync();

        // Assert
        Assert.Single(productsA);
        Assert.Equal("PROD_A", productsA[0].Code);
    }

    [Fact]
    public async Task DraftOrders_CannotAccessOtherTenantData()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        dbContext.DraftOrders.Add(new DraftOrder
        {
            DraftCode = "DRAFT_A",
            TenantId = tenantA,
            CustomerPhone = "1234567890",
            ShippingAddress = "Address A"
        });
        dbContext.DraftOrders.Add(new DraftOrder
        {
            DraftCode = "DRAFT_B",
            TenantId = tenantB,
            CustomerPhone = "0987654321",
            ShippingAddress = "Address B"
        });
        await dbContext.SaveChangesAsync();

        // Act - Query as Tenant A
        var tenantContext = new TenantContext { TenantId = tenantA, IsResolved = true };
        var dbContextA = new MessengerBotDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<MessengerBotDbContext>>(),
            tenantContext);

        var ordersA = await dbContextA.DraftOrders.ToListAsync();

        // Assert
        Assert.Single(ordersA);
        Assert.Equal("DRAFT_A", ordersA[0].DraftCode);
        Assert.DoesNotContain(ordersA, o => o.DraftCode == "DRAFT_B");
    }

    [Fact]
    public async Task CustomerIdentities_IsolatedByTenant()
    {
        // Similar test for CustomerIdentity
    }

    [Fact]
    public async Task IgnoreQueryFilters_ReturnsAllTenants()
    {
        // Verify IgnoreQueryFilters bypasses tenant filter
        // This should only be used in admin contexts with audit logging
    }
}
```

### Step 6: Add TenantId Indexes (30min)

Verify TenantId indexed on all ITenantOwnedEntity tables for query performance:

```csharp
// In MessengerBotDbContext.OnModelCreating
modelBuilder.Entity<Product>()
    .HasIndex(p => p.TenantId);

modelBuilder.Entity<DraftOrder>()
    .HasIndex(d => d.TenantId);

// ... for all ITenantOwnedEntity types
```

Generate migration:
```bash
dotnet ef migrations add AddTenantIdIndexes
```

### Step 7: Document Tenant Isolation Strategy (30min)

**File:** `docs/tenant-isolation-strategy.md`

```markdown
# Tenant Isolation Strategy

## Overview

Multi-tenant architecture with row-level security via global query filters.

## How It Works

1. TenantResolutionMiddleware extracts FacebookPageId from request
2. Looks up FacebookPageConfig to get TenantId
3. Sets TenantContext.TenantId for request scope
4. DbContext applies global query filters on all ITenantOwnedEntity types
5. Queries automatically filtered by TenantId

## Entity Types

All entities implementing ITenantOwnedEntity are automatically filtered:
- Product, Gift, ProductGiftMapping
- ConversationSession, ConversationMessage
- CustomerIdentity, DraftOrder, RiskSignal, VipProfile
- HumanSupportCase, BotConversationLock
- KnowledgeSnapshot, AdminAuditLog
- FacebookPageConfig, ManagerProfile

## Query Filter Pattern

```csharp
.HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId)
```

Logic:
- If tenant not resolved (e.g., background job): return all
- If TenantId null (e.g., system data): return
- Otherwise: return only current tenant's data

## Bypassing Filters

Use IgnoreQueryFilters() ONLY in admin contexts with audit logging:

```csharp
var allOrders = await _dbContext.DraftOrders
    .IgnoreQueryFilters()
    .ToListAsync();

await _auditService.LogAdminActionAsync("CrossTenantQuery", "DraftOrder", ...);
```

## Testing

Integration tests verify cross-tenant isolation:
- TenantIsolationTests.cs
- Each ITenantOwnedEntity type tested

## Performance

TenantId indexed on all tables for efficient filtering.
```

## Todo List

- [ ] Audit query filters completeness (all 15 entities)
- [ ] Audit CustomerIntelligenceService queries
- [ ] Audit DraftOrderService queries
- [ ] Audit SessionRepository queries
- [ ] Audit AdminDashboardQueryService for IgnoreQueryFilters
- [ ] Add missing query filters if any
- [ ] Fix services using FacebookPageId instead of TenantId
- [ ] Add audit logging for IgnoreQueryFilters usage
- [ ] Write integration tests for tenant isolation
- [ ] Add TenantId indexes if missing
- [ ] Generate migration for indexes
- [ ] Document tenant isolation strategy
- [ ] Run integration tests
- [ ] Performance test query filter overhead

## Success Criteria

- [ ] All 15 ITenantOwnedEntity types have query filters
- [ ] No services bypass filters without audit logging
- [ ] Integration tests verify cross-tenant isolation
- [ ] TenantId indexed on all tables
- [ ] Documentation complete
- [ ] Performance impact < 5% (measure with benchmarks)

## Risk Assessment

### High: Missing Query Filters Allow Data Leaks
**Likelihood:** Medium | **Impact:** Critical

If any ITenantOwnedEntity missing query filter, cross-tenant data leaks possible.

**Mitigation:**
- Comprehensive audit of all entity types
- Integration tests verify isolation
- Code review before deployment

### Medium: Performance Impact of Query Filters
**Likelihood:** Low | **Impact:** Medium

Global query filters add WHERE clause to every query.

**Mitigation:**
- TenantId already indexed on most tables
- Add missing indexes
- Benchmark query performance before/after
- Query filters compiled once, minimal overhead

### Low: Breaking Existing Queries
**Likelihood:** Low | **Impact:** Medium

Services relying on FacebookPageId filtering may break.

**Mitigation:**
- Audit all services before changes
- Integration tests verify existing behavior
- Gradual rollout with monitoring

## Security Considerations

### Defense in Depth
- Global query filters (primary defense)
- TenantContext validation (secondary defense)
- Audit logging for IgnoreQueryFilters (detection)
- Integration tests (verification)

### Admin Access
- Admin endpoints use IgnoreQueryFilters with audit logging
- AdminUserContext tracks which admin performed action
- AdminAuditLog records all cross-tenant queries

### Token Resolution
- Page access tokens resolved per tenant
- FacebookPageConfig.TenantId enforces token ownership
- No token sharing across tenants

## Next Steps

1. Audit query filters completeness
2. Audit service queries for issues
3. Fix any missing filters or audit logging
4. Write comprehensive integration tests
5. Add TenantId indexes if missing
6. Document tenant isolation strategy
7. Performance benchmark before/after
8. Deploy to staging for integration testing
