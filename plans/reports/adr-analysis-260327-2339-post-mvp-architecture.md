# ADR Analysis: Post-MVP Architecture Applicability

**Date:** 2026-03-27 23:39
**Context:** Đánh giá khả năng áp dụng các kiến trúc trong ADR sau khi hoàn thành MVP Múi Xù Cosmetics

---

## Executive Summary

**Recommendation:** Áp dụng **TỪNG BƯỚC** theo thứ tự ưu tiên. Không implement tất cả cùng lúc.

| ADR | Áp dụng? | Timing | Priority | Effort |
|-----|----------|--------|----------|--------|
| ADR-001: Multi-Tenancy | ✅ YES | Phase 5 (Week 5) | HIGH | 5 days |
| ADR-002: Request Routing | ✅ YES | Phase 5 (Week 5) | HIGH | Included |
| ADR-003: Product Schema | ⚠️ PARTIAL | Post-MVP | MEDIUM | 3 days |
| ADR-004: Caching Strategy | ✅ YES | Phase 7 (Week 7) | MEDIUM | 2 days |
| ADR-005: RAG Multi-Category | ❌ NO | Future | LOW | N/A |
| ADR-006: Security | ✅ YES | Phase 7 (Week 7) | HIGH | 3 days |

---

## ADR-001: Multi-Tenancy Pattern ✅ RECOMMENDED

### Current Status
- **MVP:** Single tenant (Múi Xù only)
- **Phase 5:** Multi-tenant architecture planned (5 days, 20M VND)

### Recommendation: Option A (Shared Schema + RLS)

**Why this fits:**
- ✅ Múi Xù là pilot customer, sẽ có thêm 5-10 tenants trong 6 tháng
- ✅ Cost-effective cho startup phase
- ✅ EF Core hỗ trợ global query filters sẵn
- ✅ PostgreSQL RLS battle-tested

**Implementation Plan:**
```csharp
// 1. Add tenant_id to all tables
public abstract class TenantEntity
{
    public Guid TenantId { get; set; }
}

// 2. Global query filter
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>()
        .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);
}

// 3. PostgreSQL RLS
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON products
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);
```

**Migration Strategy:**
1. Week 5: Add `tenant_id` column to all tables
2. Week 5: Create `tenants` and `branches` tables
3. Week 5: Implement `TenantContext` middleware
4. Week 5: Add automated tests for tenant isolation
5. Week 7: Enable PostgreSQL RLS (production hardening)

**Risk Mitigation:**
- ⚠️ Data leakage risk → Automated tests MUST verify isolation
- ⚠️ Noisy neighbor → Monitor query performance per tenant
- ⚠️ Backup complexity → Document per-tenant restore procedure

---

## ADR-002: Request Routing Strategy ✅ RECOMMENDED

### Current Status
- **MVP:** No routing (single tenant)
- **Phase 5:** PageId → Branch lookup

### Recommendation: Option A (PageId Lookup + Caching)

**Why this fits:**
- ✅ Facebook PageId là stable identifier
- ✅ Simple implementation (1 DB lookup)
- ✅ Caching giảm DB load

**Implementation:**
```csharp
public class TenantResolutionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var pageId = ExtractPageId(context.Request);

        // L1 cache: Memory (5 min)
        var branch = await _memoryCache.GetOrCreateAsync($"page:{pageId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _branchRepo.GetByPageIdAsync(pageId);
            });

        context.Items["TenantId"] = branch.TenantId;
        context.Items["BranchId"] = branch.Id;

        await _next(context);
    }
}
```

**Caching Strategy:**
- L1 (Memory): 5 min TTL → 99% hit rate
- L2 (Redis): 1 hour TTL → Fallback nếu L1 miss
- Invalidation: Khi branch config thay đổi

---

## ADR-003: Product Schema Design ⚠️ PARTIAL

### Current Status
- **MVP:** Cosmetics-specific schema (hardcoded fields)
- **Current:** `Product`, `ProductVariant`, `CosmeticsMetadata` entities

### Recommendation: DEFER polymorphic JSON

**Why NOT now:**
- ❌ MVP chỉ có cosmetics category
- ❌ Thêm complexity không cần thiết
- ❌ JSONB queries khó debug
- ❌ Client chưa có nhu cầu multi-category

**When to apply:**
- ✅ Khi có tenant thứ 2 với category khác (fashion, electronics)
- ✅ Khi có >= 3 categories cần support
- ✅ Sau khi MVP stable 3-6 tháng

**Current approach is OK:**
```csharp
// Keep current schema for MVP
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ProductCategory Category { get; set; } // Enum: Cosmetics only
}

public class CosmeticsMetadata
{
    public List<string> Ingredients { get; set; }
    public List<string> SkinTypes { get; set; }
}
```

**Future migration path:**
1. Add `MetadataJson` column (nullable)
2. Migrate existing data to JSON
3. Deprecate old columns
4. Remove old columns after 1 month

---

## ADR-004: Caching Strategy ✅ RECOMMENDED

### Current Status
- **MVP:** No caching (direct DB queries)
- **Phase 7:** Add Redis for production

### Recommendation: Hybrid (Memory + Redis)

**Why this fits:**
- ✅ Giảm DB load cho product catalog
- ✅ Session state cần distributed cache
- ✅ RAG embeddings expensive to compute

**Implementation Plan:**

**Week 7 (Production Hardening):**
```
L1: In-Memory (per instance)
    ├── Tenant config (5 min TTL)
    └── Branch routing (5 min TTL)

L2: Redis (distributed)
    ├── Product catalog (1 hour TTL)
    ├── Session state (30 min TTL)
    └── RAG embeddings (24 hour TTL)

L3: PostgreSQL (source of truth)
```

**Tenant-aware keys:**
```csharp
public class TenantAwareCache : IDistributedCache
{
    public async Task<byte[]?> GetAsync(string key, CancellationToken token)
    {
        var tenantKey = $"tenant:{_tenantContext.TenantId}:{key}";
        return await _cache.GetAsync(tenantKey, token);
    }
}
```

**Cache invalidation:**
- Write-through: Update DB + invalidate cache
- TTL-based: Automatic expiration
- Event-based: Pub/sub for real-time (future)

**Cost:**
- Redis Cloud: $10-20/month (Starter plan)
- Alternative: Redis on same VPS (free, but single point of failure)

---

## ADR-005: RAG Multi-Category ❌ NOT APPLICABLE

### Current Status
- **MVP:** Cosmetics-only RAG
- **Current:** Single embedding model (gemini-embedding-2-preview)

### Recommendation: SKIP for now

**Why NOT applicable:**
- ❌ MVP chỉ có 1 category (cosmetics)
- ❌ Category plugin system overkill
- ❌ Thêm complexity không cần thiết
- ❌ No ROI trong 12 tháng đầu

**When to revisit:**
- Khi có >= 3 categories
- Khi mỗi category có semantic space khác nhau rõ rệt
- Khi có budget để maintain multiple plugins

**Current approach is sufficient:**
```csharp
// Single RAG service for cosmetics
public class GeminiEmbeddingService
{
    public async Task<float[]> GenerateAsync(string text)
    {
        // Works fine for cosmetics domain
    }
}
```

---

## ADR-006: Security & Compliance ✅ CRITICAL

### Current Status
- **MVP:** Basic security (HTTPS, webhook verification)
- **Missing:** Encryption at rest, audit logging, RLS

### Recommendation: Implement in Phase 7

**Priority items:**

**1. Tenant Isolation (CRITICAL):**
```csharp
// Automated tests
[Fact]
public async Task ProductRepository_ShouldNotLeakDataBetweenTenants()
{
    var tenant1 = CreateTenant();
    var tenant2 = CreateTenant();
    var product1 = CreateProduct(tenant1.Id);

    SetTenantContext(tenant1.Id);
    var results = await _productRepo.GetAllAsync();

    Assert.Contains(product1, results);
    Assert.DoesNotContain(product2, results); // MUST NOT leak
}
```

**2. Data Encryption (HIGH):**
```csharp
// Encrypt sensitive fields
public class Branch
{
    [Encrypted]
    public string PageAccessToken { get; set; }

    [Encrypted]
    public string WebhookVerifyToken { get; set; }
}
```

**3. Audit Logging (MEDIUM):**
```csharp
// Log all tenant operations
_logger.LogInformation(
    "Product created: {ProductId} for tenant {TenantId}",
    product.Id, tenantContext.TenantId);
```

**4. PostgreSQL RLS (HIGH):**
```sql
-- Enable row-level security
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON products
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);
```

---

## Implementation Roadmap

### Phase 5: Multi-Tenant Foundation (Week 5)
- ✅ ADR-001: Add tenant_id to schema
- ✅ ADR-002: Implement PageId routing
- ✅ Create tenants/branches tables
- ✅ TenantContext middleware
- **Effort:** 5 days, 20M VND (already planned)

### Phase 7: Production Hardening (Week 7)
- ✅ ADR-004: Redis caching (L1 + L2)
- ✅ ADR-006: Security hardening
  - Encryption at rest
  - PostgreSQL RLS
  - Audit logging
  - Tenant isolation tests
- **Effort:** 5 days, 20M VND (add to Phase 7)

### Post-MVP (Month 3-6)
- ⚠️ ADR-003: Polymorphic product schema (if needed)
- ❌ ADR-005: Multi-category RAG (skip unless needed)

---

## Cost Impact

### Immediate (Phase 5 + 7)
- Development: 0 VND (already budgeted in Phase 5 & 7)
- Infrastructure: +$10-20/month (Redis Cloud)

### Post-MVP (if needed)
- ADR-003 migration: 3 days, 12M VND
- ADR-005 plugins: 7 days, 28M VND (only if multi-category needed)

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Tenant data leakage | CRITICAL | Automated tests + PostgreSQL RLS |
| Cache invalidation bugs | HIGH | TTL-based + monitoring |
| Redis single point of failure | MEDIUM | Redis Sentinel or managed service |
| Over-engineering too early | MEDIUM | Defer ADR-003 & ADR-005 |

---

## Decision Matrix

### Apply NOW (Phase 5-7):
- ✅ ADR-001: Multi-tenancy (business requirement)
- ✅ ADR-002: Request routing (required for multi-tenant)
- ✅ ADR-004: Caching (performance requirement)
- ✅ ADR-006: Security (compliance requirement)

### Apply LATER (Post-MVP):
- ⚠️ ADR-003: Polymorphic schema (only if multi-category)
- ❌ ADR-005: Category plugins (only if >= 3 categories)

---

## Unresolved Questions

1. **Redis hosting:** Cloud (Redis Cloud) hay self-hosted (VPS)?
   - Recommendation: Start with Redis Cloud ($10/month), migrate to self-hosted nếu cost là issue

2. **Tenant provisioning:** Manual hay self-service?
   - Recommendation: Manual for first 10 tenants, automate sau đó

3. **Backup strategy:** Per-tenant hay full database?
   - Recommendation: Full database backup daily, per-tenant restore script

4. **Monitoring:** Tenant-level metrics?
   - Recommendation: Yes, track queries/tenant, cache hit rate/tenant

---

## Conclusion

**TL;DR:**
- ✅ Implement ADR-001, 002, 004, 006 trong Phase 5-7 (already planned)
- ⚠️ Defer ADR-003 until multi-category needed
- ❌ Skip ADR-005 unless >= 3 categories

**Total additional cost:** $10-20/month (Redis), 0 VND development (already budgeted)

**Next steps:**
1. Review this analysis với client
2. Confirm Redis hosting strategy
3. Proceed with Phase 5 implementation
