# Phase 5: Testing & Validation

**Duration:** 2 days
**Cost:** 8M VND
**Status:** Not Started
**Depends on:** Phase 1, 2, 3, 4

---

## Overview

Comprehensive testing and validation of multi-tenant architecture to ensure zero data leakage, performance targets met, and production readiness.

---

## Requirements

### Functional
- All tenant isolation tests passing
- Performance benchmarks met
- Security audit completed
- Migration rollback tested

### Non-Functional
- 100% tenant isolation test coverage
- < 5ms tenant resolution overhead
- > 95% cache hit rate
- Zero data leakage incidents

---

## Testing Strategy

### Test Pyramid
```
E2E Tests (10%)
    ├── Multi-tenant webhook flow
    └── Cross-tenant isolation

Integration Tests (30%)
    ├── Tenant resolution
    ├── Query filters
    ├── Cache behavior
    └── RLS enforcement

Unit Tests (60%)
    ├── TenantContext
    ├── Repositories
    ├── Middleware
    └── Services
```

---

## Implementation Steps

### Step 1: Unit Tests (4 hours)

#### 1.1 TenantContext Tests
```csharp
// tests/MessengerWebhook.Tests/Services/TenantContextTests.cs
public class TenantContextTests
{
    [Fact]
    public void TenantContext_WhenNotResolved_ShouldThrowException()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        var tenantContext = new TenantContext(httpContextAccessor);

        var ex = Assert.Throws<InvalidOperationException>(() => tenantContext.TenantId);
        Assert.Contains("Tenant context not resolved", ex.Message);
    }

    [Fact]
    public void TenantContext_WhenResolved_ShouldReturnTenantId()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = tenantId;
        httpContext.Items["BranchId"] = Guid.NewGuid();

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var tenantContext = new TenantContext(httpContextAccessor);

        Assert.Equal(tenantId, tenantContext.TenantId);
        Assert.True(tenantContext.IsResolved);
    }
}
```

#### 1.2 Repository Tests
```csharp
// tests/MessengerWebhook.Tests/Data/Repositories/ProductRepositoryTests.cs
public class ProductRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ShouldApplyTenantFilter()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var mockContext = CreateMockDbContext();
        mockContext.Products.AddRange(
            new Product { Id = Guid.NewGuid(), TenantId = tenant1 },
            new Product { Id = Guid.NewGuid(), TenantId = tenant2 }
        );

        var tenantContext = CreateTenantContext(tenant1);
        var repo = new ProductRepository(mockContext, tenantContext);

        // Act
        var results = await repo.GetAllAsync();

        // Assert
        Assert.All(results, p => Assert.Equal(tenant1, p.TenantId));
    }
}
```

---

### Step 2: Integration Tests (6 hours)

#### 2.1 Tenant Resolution Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Middleware/TenantResolutionIntegrationTests.cs
public class TenantResolutionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Webhook_WithValidPageId_ShouldResolveTenant()
    {
        // Arrange
        var tenant = await CreateTenantAsync("Test Tenant");
        var branch = await CreateBranchAsync(tenant.Id, "123456789");

        var webhook = new WebhookEvent
        {
            Entry = new[] { new Entry { Id = "123456789" } }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/webhook", webhook);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify tenant context was set correctly
        var logs = GetLogs();
        Assert.Contains(logs, l => l.Contains($"Resolved tenant: {tenant.Name}"));
    }

    [Fact]
    public async Task Webhook_WithInvalidPageId_ShouldReturn404()
    {
        var webhook = new WebhookEvent
        {
            Entry = new[] { new Entry { Id = "invalid-page-id" } }
        };

        var response = await _client.PostAsJsonAsync("/webhook", webhook);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithCachedPageId_ShouldNotHitDatabase()
    {
        // First request: cache miss
        var tenant = await CreateTenantAsync("Test Tenant");
        var branch = await CreateBranchAsync(tenant.Id, "123456789");
        var webhook = new WebhookEvent
        {
            Entry = new[] { new Entry { Id = "123456789" } }
        };

        await _client.PostAsJsonAsync("/webhook", webhook);

        // Second request: cache hit
        var dbQueryCountBefore = GetDatabaseQueryCount();
        await _client.PostAsJsonAsync("/webhook", webhook);
        var dbQueryCountAfter = GetDatabaseQueryCount();

        Assert.Equal(dbQueryCountBefore, dbQueryCountAfter); // No DB queries
    }
}
```

#### 2.2 Query Filter Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Data/QueryFilterIntegrationTests.cs
public class QueryFilterIntegrationTests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Products_ShouldOnlyReturnCurrentTenantData()
    {
        // Arrange
        var tenant1 = await CreateTenantAsync("Tenant 1");
        var tenant2 = await CreateTenantAsync("Tenant 2");

        var product1 = await CreateProductAsync(tenant1.Id, "Product 1");
        var product2 = await CreateProductAsync(tenant2.Id, "Product 2");

        // Act: Query as Tenant 1
        SetTenantContext(tenant1.Id);
        var results = await _context.Products.ToListAsync();

        // Assert
        Assert.Contains(product1, results);
        Assert.DoesNotContain(product2, results);
    }

    [Fact]
    public async Task Sessions_ShouldOnlyReturnCurrentTenantData()
    {
        var tenant1 = await CreateTenantAsync("Tenant 1");
        var tenant2 = await CreateTenantAsync("Tenant 2");

        var session1 = await CreateSessionAsync(tenant1.Id, "user1");
        var session2 = await CreateSessionAsync(tenant2.Id, "user2");

        SetTenantContext(tenant1.Id);
        var results = await _context.ConversationSessions.ToListAsync();

        Assert.Contains(session1, results);
        Assert.DoesNotContain(session2, results);
    }

    [Fact]
    public async Task JoinQueries_ShouldRespectTenantFilter()
    {
        var tenant1 = await CreateTenantAsync("Tenant 1");
        var tenant2 = await CreateTenantAsync("Tenant 2");

        var product1 = await CreateProductAsync(tenant1.Id, "Product 1");
        var variant1 = await CreateVariantAsync(product1.Id, tenant1.Id);

        var product2 = await CreateProductAsync(tenant2.Id, "Product 2");
        var variant2 = await CreateVariantAsync(product2.Id, tenant2.Id);

        SetTenantContext(tenant1.Id);
        var results = await _context.Products
            .Include(p => p.Variants)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal(product1.Id, results[0].Id);
        Assert.All(results[0].Variants, v => Assert.Equal(tenant1.Id, v.TenantId));
    }
}
```

#### 2.3 Cache Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Caching/HybridCacheIntegrationTests.cs
public class HybridCacheIntegrationTests
{
    [Fact]
    public async Task Cache_L1Hit_ShouldNotQueryL2()
    {
        var key = "test:key";
        var value = new { Data = "test" };

        await _cache.SetAsync(key, value);

        // L1 should have it
        var result = await _cache.GetAsync<object>(key);

        Assert.NotNull(result);
        _redisMock.Verify(r => r.GetStringAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Cache_L1Miss_ShouldQueryL2()
    {
        var key = "test:key";
        var value = new { Data = "test" };

        // Set in L2 only
        await _redis.SetStringAsync(key, JsonSerializer.Serialize(value));

        // Clear L1
        _memoryCache.Remove(key);

        // Should hit L2
        var result = await _cache.GetAsync<object>(key);

        Assert.NotNull(result);
        _redisMock.Verify(r => r.GetStringAsync(It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task Cache_TenantAware_ShouldIsolateKeys()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        SetTenantContext(tenant1);
        await _cache.SetAsync("product:123", new { Name = "Product 1" });

        SetTenantContext(tenant2);
        var result = await _cache.GetAsync<object>("product:123");

        Assert.Null(result); // Different tenant, should not see it
    }
}
```

#### 2.4 RLS Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Security/RowLevelSecurityTests.cs
public class RowLevelSecurityTests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task RLS_WithoutTenantContext_ShouldBlockAllQueries()
    {
        var tenant = await CreateTenantAsync("Test Tenant");
        await CreateProductAsync(tenant.Id, "Product");

        // Don't set tenant context
        var results = await _context.Products.ToListAsync();

        Assert.Empty(results); // RLS blocks everything
    }

    [Fact]
    public async Task RLS_WithWrongTenantContext_ShouldBlockQueries()
    {
        var tenant1 = await CreateTenantAsync("Tenant 1");
        var tenant2 = await CreateTenantAsync("Tenant 2");

        await CreateProductAsync(tenant1.Id, "Product 1");

        SetTenantContext(tenant2.Id);
        var results = await _context.Products.ToListAsync();

        Assert.Empty(results); // RLS blocks cross-tenant access
    }

    [Fact]
    public async Task RLS_DirectSQL_ShouldRespectPolicy()
    {
        var tenant1 = await CreateTenantAsync("Tenant 1");
        var tenant2 = await CreateTenantAsync("Tenant 2");

        await CreateProductAsync(tenant1.Id, "Product 1");
        await CreateProductAsync(tenant2.Id, "Product 2");

        // Set tenant context
        await _context.Database.ExecuteSqlRawAsync(
            "SET LOCAL app.current_tenant_id = {0}", tenant1.Id);

        // Direct SQL query
        var results = await _context.Products
            .FromSqlRaw("SELECT * FROM products")
            .ToListAsync();

        Assert.All(results, p => Assert.Equal(tenant1.Id, p.TenantId));
    }
}
```

---

### Step 3: E2E Tests (2 hours)

```csharp
// tests/MessengerWebhook.E2ETests/MultiTenantWebhookFlowTests.cs
public class MultiTenantWebhookFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CompleteFlow_Tenant1_ShouldNotAffectTenant2()
    {
        // Setup two tenants
        var tenant1 = await CreateTenantAsync("Tenant 1");
        var branch1 = await CreateBranchAsync(tenant1.Id, "page1");

        var tenant2 = await CreateTenantAsync("Tenant 2");
        var branch2 = await CreateBranchAsync(tenant2.Id, "page2");

        // Tenant 1: Send message
        var webhook1 = CreateWebhook("page1", "user1", "Hello");
        await _client.PostAsJsonAsync("/webhook", webhook1);

        // Tenant 2: Send message
        var webhook2 = CreateWebhook("page2", "user2", "Hi");
        await _client.PostAsJsonAsync("/webhook", webhook2);

        // Verify: Tenant 1 sessions
        SetTenantContext(tenant1.Id);
        var sessions1 = await _context.ConversationSessions.ToListAsync();
        Assert.Single(sessions1);
        Assert.Equal("user1", sessions1[0].PageScopedUserId);

        // Verify: Tenant 2 sessions
        SetTenantContext(tenant2.Id);
        var sessions2 = await _context.ConversationSessions.ToListAsync();
        Assert.Single(sessions2);
        Assert.Equal("user2", sessions2[0].PageScopedUserId);
    }
}
```

---

### Step 4: Performance Tests (3 hours)

```csharp
// tests/MessengerWebhook.PerformanceTests/TenantResolutionBenchmarks.cs
[MemoryDiagnoser]
public class TenantResolutionBenchmarks
{
    [Benchmark]
    public async Task TenantResolution_CacheHit()
    {
        // Measure tenant resolution with cache hit
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("123456789");

        await middleware.InvokeAsync(context);
    }

    [Benchmark]
    public async Task TenantResolution_CacheMiss()
    {
        // Measure tenant resolution with cache miss
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("new-page-id");

        await middleware.InvokeAsync(context);
    }

    [Benchmark]
    public async Task QueryWithFilter_100Products()
    {
        // Measure query performance with global filter
        SetTenantContext(Guid.NewGuid());
        var results = await _context.Products.Take(100).ToListAsync();
    }
}
```

**Expected results:**
- Tenant resolution (cache hit): < 2ms
- Tenant resolution (cache miss): < 10ms
- Query with filter: < 50ms for 100 products

---

### Step 5: Security Audit (3 hours)

#### 5.1 Manual Security Checklist
```markdown
- [ ] RLS enabled on all tenant tables
- [ ] RLS policies tested manually
- [ ] Sensitive fields encrypted (API keys, tokens)
- [ ] Audit logging captures all operations
- [ ] No hardcoded tenant IDs in code
- [ ] No SQL injection vulnerabilities
- [ ] Cache keys properly namespaced
- [ ] Error messages don't leak tenant info
```

#### 5.2 Automated Security Tests
```csharp
// tests/MessengerWebhook.SecurityTests/TenantIsolationSecurityTests.cs
public class TenantIsolationSecurityTests
{
    [Fact]
    public async Task SqlInjection_InPageId_ShouldNotBypassTenantFilter()
    {
        var maliciousPageId = "123' OR '1'='1";
        var webhook = CreateWebhook(maliciousPageId, "user", "test");

        var response = await _client.PostAsJsonAsync("/webhook", webhook);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DirectDatabaseAccess_WithoutTenantContext_ShouldFail()
    {
        // Try to query without setting tenant context
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _context.Products.ToListAsync());

        Assert.Contains("Tenant context not resolved", exception.Message);
    }
}
```

---

### Step 6: Migration Rollback Test (2 hours)

```bash
# Test rollback procedure
# 1. Backup current state
pg_dump -h localhost -U postgres -d messenger_bot > backup_test.sql

# 2. Apply migration
dotnet ef database update

# 3. Verify migration
dotnet test --filter "Category=Migration"

# 4. Rollback
dotnet ef database update <previous-migration>

# 5. Verify rollback
psql -h localhost -U postgres -d messenger_bot -c "SELECT * FROM tenants"
# Should fail (table doesn't exist)

# 6. Restore from backup
psql -h localhost -U postgres -d messenger_bot < backup_test.sql
```

---

## Test Coverage Report

```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=html

# Expected coverage:
# - Overall: > 80%
# - Tenant isolation code: 100%
# - Security code: 100%
```

---

## Success Criteria

- ✅ All unit tests passing (100%)
- ✅ All integration tests passing (100%)
- ✅ All E2E tests passing (100%)
- ✅ Performance benchmarks met
- ✅ Security audit completed
- ✅ Test coverage > 80%
- ✅ Zero data leakage incidents
- ✅ Migration rollback tested

---

## Performance Targets

| Metric | Target | Actual |
|--------|--------|--------|
| Tenant resolution (cache hit) | < 2ms | TBD |
| Tenant resolution (cache miss) | < 10ms | TBD |
| Cache hit rate | > 95% | TBD |
| Query overhead (global filter) | < 5ms | TBD |
| RLS overhead | < 5ms | TBD |

---

## Final Checklist

### Code Quality
- [ ] All tests passing
- [ ] No compiler warnings
- [ ] Code review completed
- [ ] Documentation updated

### Security
- [ ] RLS enabled and tested
- [ ] Encryption working
- [ ] Audit logging verified
- [ ] Security checklist completed

### Performance
- [ ] Benchmarks run
- [ ] Cache hit rate measured
- [ ] No performance regressions

### Operations
- [ ] Migration tested
- [ ] Rollback tested
- [ ] Monitoring configured
- [ ] Runbook updated

---

## Next Steps

After Phase 5 completion:
- Deploy to staging environment
- Run smoke tests
- Monitor for 24 hours
- Deploy to production
