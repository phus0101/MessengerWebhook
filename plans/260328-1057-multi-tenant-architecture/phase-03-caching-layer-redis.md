# Phase 3: Caching Layer (Redis)

**Duration:** 2 days
**Cost:** 8M VND
**Status:** Not Started
**Depends on:** Phase 2

---

## Overview

Add distributed caching with Redis to reduce database load and improve performance for multi-tenant routing.

---

## Requirements

### Functional
- L1 cache: In-memory (per instance)
- L2 cache: Redis (distributed)
- Tenant-aware cache keys
- Cache invalidation strategy

### Non-Functional
- > 95% cache hit rate for routing
- < 2ms cache lookup latency
- Automatic failover if Redis unavailable

---

## Architecture

### Caching Layers
```
Request → L1 (Memory) → L2 (Redis) → Database
           5 min TTL      1 hour TTL
```

**What to cache:**
- Tenant/Branch routing (hot path)
- Product catalog (read-heavy)
- Session state (distributed)
- RAG embeddings (expensive to compute)

---

## Implementation Steps

### Step 1: Setup Redis (1 hour)

**Option A: Redis Cloud (Recommended)**
```bash
# Sign up at redis.com/try-free
# Get connection string: redis://default:password@host:port
```

**Option B: Self-hosted (VPS)**
```bash
# Install Redis
sudo apt update
sudo apt install redis-server

# Configure
sudo nano /etc/redis/redis.conf
# Set: bind 0.0.0.0
# Set: requirepass your-strong-password

# Start
sudo systemctl start redis
sudo systemctl enable redis
```

### Step 2: Add NuGet Packages (15 min)
```bash
cd src/MessengerWebhook
dotnet add package StackExchange.Redis
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

### Step 3: Create Redis Configuration (30 min)
```csharp
// src/MessengerWebhook/Configuration/RedisOptions.cs
public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "messenger_bot:";
    public bool Enabled { get; set; } = true;
    public int DefaultTtlMinutes { get; set; } = 60;
}
```

```json
// appsettings.json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "messenger_bot:",
    "Enabled": true,
    "DefaultTtlMinutes": 60
  }
}
```

### Step 4: Create TenantAwareCache (2 hours)
```csharp
// src/MessengerWebhook/Services/Caching/ITenantAwareCache.cs
public interface ITenantAwareCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

// src/MessengerWebhook/Services/Caching/TenantAwareCache.cs
public class TenantAwareCache : ITenantAwareCache
{
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantAwareCache> _logger;
    private readonly RedisOptions _options;

    public TenantAwareCache(
        IDistributedCache cache,
        ITenantContext tenantContext,
        IOptions<RedisOptions> options,
        ILogger<TenantAwareCache> logger)
    {
        _cache = cache;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var tenantKey = BuildKey(key);
        var json = await _cache.GetStringAsync(tenantKey, ct);

        if (json == null)
        {
            _logger.LogDebug("Cache miss: {Key}", tenantKey);
            return default;
        }

        _logger.LogDebug("Cache hit: {Key}", tenantKey);
        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct)
    {
        var tenantKey = BuildKey(key);
        var json = JsonSerializer.Serialize(value);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(_options.DefaultTtlMinutes)
        };

        await _cache.SetStringAsync(tenantKey, json, options, ct);
        _logger.LogDebug("Cache set: {Key} (TTL: {Ttl})", tenantKey, options.AbsoluteExpirationRelativeToNow);
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var tenantKey = BuildKey(key);
        await _cache.RemoveAsync(tenantKey, ct);
        _logger.LogDebug("Cache removed: {Key}", tenantKey);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct)
    {
        // Redis-specific: requires IConnectionMultiplexer
        // For now, log warning
        _logger.LogWarning("RemoveByPrefix not implemented for distributed cache");
    }

    private string BuildKey(string key)
    {
        if (!_tenantContext.IsResolved)
            return $"global:{key}";

        return $"tenant:{_tenantContext.TenantId}:{key}";
    }
}
```

### Step 5: Create Hybrid Cache (L1 + L2) (2 hours)
```csharp
// src/MessengerWebhook/Services/Caching/HybridCache.cs
public class HybridCache : ITenantAwareCache
{
    private readonly IMemoryCache _l1Cache;
    private readonly IDistributedCache _l2Cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<HybridCache> _logger;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var tenantKey = BuildKey(key);

        // L1: Memory cache
        if (_l1Cache.TryGetValue<T>(tenantKey, out var cachedValue))
        {
            _logger.LogDebug("L1 cache hit: {Key}", tenantKey);
            return cachedValue;
        }

        // L2: Redis
        var json = await _l2Cache.GetStringAsync(tenantKey, ct);
        if (json != null)
        {
            _logger.LogDebug("L2 cache hit: {Key}", tenantKey);
            var value = JsonSerializer.Deserialize<T>(json);

            // Populate L1
            _l1Cache.Set(tenantKey, value, TimeSpan.FromMinutes(5));
            return value;
        }

        _logger.LogDebug("Cache miss (L1+L2): {Key}", tenantKey);
        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct)
    {
        var tenantKey = BuildKey(key);

        // Set L1 (5 min)
        _l1Cache.Set(tenantKey, value, TimeSpan.FromMinutes(5));

        // Set L2 (custom TTL)
        var json = JsonSerializer.Serialize(value);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromHours(1)
        };
        await _l2Cache.SetStringAsync(tenantKey, json, options, ct);

        _logger.LogDebug("Cache set (L1+L2): {Key}", tenantKey);
    }

    private string BuildKey(string key)
    {
        if (!_tenantContext.IsResolved)
            return $"global:{key}";
        return $"tenant:{_tenantContext.TenantId}:{key}";
    }
}
```

### Step 6: Update TenantResolutionMiddleware (1 hour)
```csharp
// Use HybridCache instead of IMemoryCache
public async Task InvokeAsync(
    HttpContext context,
    IBranchRepository branchRepo,
    ITenantAwareCache cache)
{
    var pageId = await ExtractPageIdAsync(context.Request);

    // Use hybrid cache
    var branch = await cache.GetAsync<Branch>($"page:{pageId}");
    if (branch == null)
    {
        branch = await branchRepo.GetByPageIdAsync(pageId);
        if (branch != null)
        {
            await cache.SetAsync($"page:{pageId}", branch, TimeSpan.FromMinutes(5));
        }
    }

    // ... rest of middleware
}
```

### Step 7: Register Services (30 min)
```csharp
// src/MessengerWebhook/Program.cs
var redisOptions = builder.Configuration.GetSection("Redis").Get<RedisOptions>();

if (redisOptions?.Enabled == true)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisOptions.ConnectionString;
        options.InstanceName = redisOptions.InstanceName;
    });

    builder.Services.AddSingleton<ITenantAwareCache, HybridCache>();
}
else
{
    // Fallback: Memory cache only
    builder.Services.AddSingleton<ITenantAwareCache, MemoryCacheAdapter>();
}
```

### Step 8: Cache Product Catalog (1 hour)
```csharp
// src/MessengerWebhook/Data/Repositories/ProductRepository.cs
public class ProductRepository : IProductRepository
{
    private readonly MessengerBotDbContext _context;
    private readonly ITenantAwareCache _cache;

    public async Task<List<Product>> GetAllAsync(CancellationToken ct)
    {
        var cacheKey = "products:all";

        var cached = await _cache.GetAsync<List<Product>>(cacheKey, ct);
        if (cached != null)
            return cached;

        var products = await _context.Products
            .Include(p => p.Variants)
            .ToListAsync(ct);

        await _cache.SetAsync(cacheKey, products, TimeSpan.FromHours(1), ct);
        return products;
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken ct)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync(ct);

        // Invalidate cache
        await _cache.RemoveAsync("products:all", ct);

        return product;
    }
}
```

---

## Cache Invalidation Strategy

### Write-through
```csharp
// On create/update/delete
await _cache.RemoveAsync($"products:all");
await _cache.RemoveAsync($"product:{productId}");
```

### TTL-based
- Routing: 5 min (L1), 1 hour (L2)
- Products: 1 hour
- Sessions: 30 min
- Embeddings: 24 hours

### Event-based (Future)
```csharp
// Pub/sub for real-time invalidation
await _redis.PublishAsync("cache:invalidate", $"tenant:{tenantId}:products");
```

---

## Testing

### Unit Tests
```csharp
[Fact]
public async Task HybridCache_L1Hit_ShouldNotQueryL2()
{
    var key = "test:key";
    var value = "test-value";

    await _cache.SetAsync(key, value);
    var result = await _cache.GetAsync<string>(key);

    Assert.Equal(value, result);
    _l2CacheMock.Verify(x => x.GetStringAsync(It.IsAny<string>(), default), Times.Never);
}

[Fact]
public async Task HybridCache_L1Miss_ShouldQueryL2()
{
    var key = "test:key";
    var value = "test-value";

    _l2CacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
        .ReturnsAsync(JsonSerializer.Serialize(value));

    var result = await _cache.GetAsync<string>(key);

    Assert.Equal(value, result);
    _l2CacheMock.Verify(x => x.GetStringAsync(It.IsAny<string>(), default), Times.Once);
}
```

### Integration Tests
```csharp
[Fact]
public async Task Redis_ShouldPersistAcrossInstances()
{
    // Instance 1: Set
    await _cache1.SetAsync("test:key", "value");

    // Instance 2: Get
    var result = await _cache2.GetAsync<string>("test:key");

    Assert.Equal("value", result);
}
```

---

## Monitoring

### Metrics to track
```csharp
_metrics.RecordCacheHit("L1");
_metrics.RecordCacheMiss("L2");
_metrics.RecordCacheLatency(stopwatch.ElapsedMilliseconds);
```

### Expected performance
- L1 hit rate: > 90%
- L2 hit rate: > 95%
- Cache latency: < 2ms

---

## Success Criteria

- ✅ Redis connected and operational
- ✅ L1 + L2 caching working
- ✅ Tenant-aware cache keys
- ✅ Cache hit rate > 95%
- ✅ Graceful fallback if Redis unavailable
- ✅ Cache invalidation on writes

---

## Cost

**Redis Cloud Starter:** $10-20/month
- 30MB storage
- 30 connections
- 99.9% uptime SLA

**Self-hosted (VPS):** $0 (use existing VPS)
- Requires maintenance
- No SLA

---

## Next Steps

After Phase 3 completion:
- Phase 4: Security hardening (RLS, encryption)
