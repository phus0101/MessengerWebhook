# Phase 2: Request Routing & Context

**Duration:** 2 days
**Cost:** 8M VND
**Status:** Not Started
**Depends on:** Phase 1

---

## Overview

Implement tenant resolution from Facebook webhook requests and enforce tenant isolation at application level.

---

## Requirements

### Functional
- Extract PageId from webhook → lookup Branch → resolve Tenant
- Store tenant context per request
- EF Core global query filters for tenant isolation
- Memory cache for routing (L1)

### Non-Functional
- < 5ms overhead for tenant resolution
- > 95% cache hit rate
- Zero data leakage between tenants

---

## Architecture

### Request Flow
```
Webhook POST /webhook
    ↓
TenantResolutionMiddleware
    ↓ Extract PageId from webhook.entry[0].id
    ↓ Check L1 cache (Memory)
    ↓ If miss: Query database
    ↓ Store in cache (5 min TTL)
    ↓
Set HttpContext.Items["TenantId"]
Set HttpContext.Items["BranchId"]
    ↓
TenantContext (scoped service)
    ↓
EF Core global query filter
    ↓
Process webhook
```

---

## Implementation Steps

### Step 1: Create ITenantContext Interface (30 min)
```csharp
// src/MessengerWebhook/Services/Tenants/ITenantContext.cs
public interface ITenantContext
{
    Guid TenantId { get; }
    Guid BranchId { get; }
    string TenantName { get; }
    bool IsResolved { get; }
}
```

### Step 2: Implement TenantContext (1 hour)
```csharp
// src/MessengerWebhook/Services/Tenants/TenantContext.cs
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var tenantId = _httpContextAccessor.HttpContext?.Items["TenantId"] as Guid?;
            if (!tenantId.HasValue)
                throw new InvalidOperationException("Tenant context not resolved");
            return tenantId.Value;
        }
    }

    public Guid BranchId
    {
        get
        {
            var branchId = _httpContextAccessor.HttpContext?.Items["BranchId"] as Guid?;
            if (!branchId.HasValue)
                throw new InvalidOperationException("Branch context not resolved");
            return branchId.Value;
        }
    }

    public string TenantName =>
        _httpContextAccessor.HttpContext?.Items["TenantName"] as string ?? "Unknown";

    public bool IsResolved =>
        _httpContextAccessor.HttpContext?.Items.ContainsKey("TenantId") == true;
}
```

### Step 3: Create Branch Repository (1 hour)
```csharp
// src/MessengerWebhook/Data/Repositories/IBranchRepository.cs
public interface IBranchRepository
{
    Task<Branch?> GetByPageIdAsync(string facebookPageId, CancellationToken ct = default);
    Task<Branch?> GetByIdAsync(Guid branchId, CancellationToken ct = default);
}

// src/MessengerWebhook/Data/Repositories/BranchRepository.cs
public class BranchRepository : IBranchRepository
{
    private readonly MessengerBotDbContext _context;

    public BranchRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<Branch?> GetByPageIdAsync(string facebookPageId, CancellationToken ct)
    {
        return await _context.Branches
            .Include(b => b.Tenant)
            .FirstOrDefaultAsync(b =>
                b.FacebookPageId == facebookPageId &&
                b.IsActive &&
                b.Tenant.IsActive, ct);
    }

    public async Task<Branch?> GetByIdAsync(Guid branchId, CancellationToken ct)
    {
        return await _context.Branches
            .Include(b => b.Tenant)
            .FirstOrDefaultAsync(b => b.Id == branchId, ct);
    }
}
```

### Step 4: Create TenantResolutionMiddleware (2 hours)
```csharp
// src/MessengerWebhook/Middleware/TenantResolutionMiddleware.cs
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IBranchRepository branchRepo,
        IMemoryCache cache)
    {
        // Skip for non-webhook requests
        if (!context.Request.Path.StartsWithSegments("/webhook"))
        {
            await _next(context);
            return;
        }

        // Extract PageId from webhook
        var pageId = await ExtractPageIdAsync(context.Request);
        if (string.IsNullOrEmpty(pageId))
        {
            _logger.LogWarning("Could not extract PageId from webhook");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid webhook payload");
            return;
        }

        // L1 cache: Memory (5 min TTL)
        var cacheKey = $"page:{pageId}";
        var branch = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            entry.SetPriority(CacheItemPriority.High);

            var b = await branchRepo.GetByPageIdAsync(pageId);
            if (b == null)
            {
                _logger.LogError("Branch not found for PageId: {PageId}", pageId);
            }
            return b;
        });

        if (branch == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Branch not found");
            return;
        }

        // Set tenant context
        context.Items["TenantId"] = branch.TenantId;
        context.Items["BranchId"] = branch.Id;
        context.Items["TenantName"] = branch.Tenant.Name;

        _logger.LogInformation(
            "Resolved tenant: {TenantName} (PageId: {PageId})",
            branch.Tenant.Name, pageId);

        await _next(context);
    }

    private async Task<string?> ExtractPageIdAsync(HttpRequest request)
    {
        // Enable buffering to read body multiple times
        request.EnableBuffering();

        try
        {
            var body = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Position = 0; // Reset for next middleware

            var webhook = JsonSerializer.Deserialize<WebhookEvent>(body);
            return webhook?.Entry?.FirstOrDefault()?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload");
            return null;
        }
    }
}
```

### Step 5: Update DbContext with Global Query Filters (2 hours)
```csharp
// src/MessengerWebhook/Data/MessengerBotDbContext.cs
public class MessengerBotDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public MessengerBotDbContext(
        DbContextOptions<MessengerBotDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global query filter to all TenantEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(TenantQueryFilterExtensions)
                    .GetMethod(nameof(TenantQueryFilterExtensions.ApplyTenantFilter))
                    ?.MakeGenericMethod(entityType.ClrType);

                method?.Invoke(null, new object[] { modelBuilder, _tenantContext });
            }
        }
    }
}

// src/MessengerWebhook/Data/Extensions/TenantQueryFilterExtensions.cs
public static class TenantQueryFilterExtensions
{
    public static void ApplyTenantFilter<T>(
        ModelBuilder modelBuilder,
        ITenantContext tenantContext) where T : TenantEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            !tenantContext.IsResolved || e.TenantId == tenantContext.TenantId);
    }
}
```

### Step 6: Register Services (30 min)
```csharp
// src/MessengerWebhook/Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IBranchRepository, BranchRepository>();

// Add middleware
app.UseMiddleware<TenantResolutionMiddleware>();
```

### Step 7: Update Repositories (1 hour)
Remove manual `tenant_id` filters (now handled by global filter):

**Before:**
```csharp
public async Task<List<Product>> GetAllAsync()
{
    return await _context.Products
        .Where(p => p.TenantId == _tenantContext.TenantId) // ❌ Remove
        .ToListAsync();
}
```

**After:**
```csharp
public async Task<List<Product>> GetAllAsync()
{
    return await _context.Products
        .ToListAsync(); // ✅ Global filter applies automatically
}
```

Apply to all repositories.

---

## Testing

### Unit Tests
```csharp
// tests/MessengerWebhook.Tests/Services/TenantContextTests.cs
[Fact]
public void TenantContext_WhenNotResolved_ShouldThrowException()
{
    var httpContextAccessor = new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext()
    };
    var tenantContext = new TenantContext(httpContextAccessor);

    Assert.Throws<InvalidOperationException>(() => tenantContext.TenantId);
}

[Fact]
public void TenantContext_WhenResolved_ShouldReturnTenantId()
{
    var tenantId = Guid.NewGuid();
    var httpContext = new DefaultHttpContext();
    httpContext.Items["TenantId"] = tenantId;

    var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
    var tenantContext = new TenantContext(httpContextAccessor);

    Assert.Equal(tenantId, tenantContext.TenantId);
}
```

### Integration Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Middleware/TenantResolutionTests.cs
[Fact]
public async Task Middleware_WithValidPageId_ShouldResolveTenant()
{
    // Arrange
    var tenant = await CreateTenantAsync();
    var branch = await CreateBranchAsync(tenant.Id, "123456789");

    var webhook = new WebhookEvent
    {
        Entry = new[] { new Entry { Id = "123456789" } }
    };

    // Act
    var response = await _client.PostAsJsonAsync("/webhook", webhook);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    // Verify tenant context was set
}

[Fact]
public async Task Middleware_WithInvalidPageId_ShouldReturn404()
{
    var webhook = new WebhookEvent
    {
        Entry = new[] { new Entry { Id = "invalid-page-id" } }
    };

    var response = await _client.PostAsJsonAsync("/webhook", webhook);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

### Query Filter Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Data/TenantQueryFilterTests.cs
[Fact]
public async Task ProductRepository_ShouldOnlyReturnCurrentTenantProducts()
{
    // Arrange
    var tenant1 = await CreateTenantAsync();
    var tenant2 = await CreateTenantAsync();
    var product1 = await CreateProductAsync(tenant1.Id);
    var product2 = await CreateProductAsync(tenant2.Id);

    // Act
    SetTenantContext(tenant1.Id);
    var results = await _productRepo.GetAllAsync();

    // Assert
    Assert.Contains(product1, results);
    Assert.DoesNotContain(product2, results); // ✅ Tenant isolation
}
```

---

## Performance Monitoring

### Metrics to track
```csharp
// Add metrics
_metrics.RecordTenantResolutionTime(stopwatch.ElapsedMilliseconds);
_metrics.RecordCacheHitRate(cacheHit ? 1 : 0);
```

### Expected performance
- Tenant resolution: < 5ms (with cache)
- Cache hit rate: > 95%
- Database queries: < 1% of requests

---

## Success Criteria

- ✅ Tenant resolved from PageId in < 5ms
- ✅ Cache hit rate > 95%
- ✅ Global query filters applied to all entities
- ✅ Zero manual `tenant_id` filters in repositories
- ✅ Tenant isolation verified by tests
- ✅ Invalid PageId returns 404

---

## Security Considerations

- Validate PageId format before database lookup
- Log all tenant resolution attempts
- Monitor for suspicious PageId patterns
- Rate limit per PageId

---

## Next Steps

After Phase 2 completion:
- Phase 3: Add Redis for L2 cache
- Phase 4: Implement PostgreSQL RLS
