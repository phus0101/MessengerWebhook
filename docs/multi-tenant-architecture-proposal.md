# Multi-Tenant, Multi-Branch, Multi-Category Architecture Proposal

**Status**: Proposal
**Created**: 2026-03-21
**Effort**: 12-18 tháng
**Risk**: High

---

## Executive Summary

Redesign toàn bộ kiến trúc để hỗ trợ:
- **Multi-tenant**: Nhiều cửa hàng độc lập trên cùng 1 hệ thống
- **Multi-branch**: Mỗi cửa hàng có nhiều chi nhánh, mỗi chi nhánh có fanpage riêng
- **Multi-category**: Hỗ trợ nhiều loại sản phẩm (mỹ phẩm, thời trang, điện tử, etc.)

**Complexity**: Tăng từ **Simple** → **Enterprise-grade**
**Timeline**: 12-18 tháng (vs 3 tháng hiện tại)
**Team size**: Cần 3-5 developers (vs 1 hiện tại)

---

## Architecture Overview

### Current vs Proposed

| Aspect | Current | Proposed |
|--------|---------|----------|
| Tenancy | Single tenant | Multi-tenant với row-level security |
| Configuration | Hardcoded | Dynamic per tenant/branch |
| Database | Single schema | Shared schema + tenant isolation |
| Routing | N/A | PSID → Branch → Tenant mapping |
| Product schema | Cosmetics-specific | Polymorphic + category plugins |
| RAG | Single model | Per-category embeddings |
| Caching | Global MemoryCache | Tenant-aware distributed cache |
| Scaling | Vertical | Horizontal + auto-scaling |

---

## Core Entities

### 1. Tenant (Cửa hàng)

```csharp
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Slug { get; set; }  // URL-friendly identifier
    public TenantStatus Status { get; set; }  // Active, Suspended, Trial
    public DateTime CreatedAt { get; set; }
    public TenantConfiguration Configuration { get; set; }

    // Relationships
    public List<Branch> Branches { get; set; }
    public List<Product> Products { get; set; }
}

public class TenantConfiguration
{
    public Guid TenantId { get; set; }
    public ProductCategory PrimaryCategory { get; set; }
    public string SystemPromptPath { get; set; }
    public string EmbeddingModel { get; set; }
    public GeminiOptions GeminiOptions { get; set; }
    public Dictionary<string, string> CustomSettings { get; set; }
}
```

### 2. Branch (Chi nhánh)

```csharp
public class Branch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public GeoLocation Location { get; set; }
    public BranchStatus Status { get; set; }

    // Facebook integration
    public string FacebookPageId { get; set; }
    public string PageAccessToken { get; set; }  // Encrypted
    public string WebhookVerifyToken { get; set; }

    // Relationships
    public Tenant Tenant { get; set; }
    public List<BranchInventory> Inventory { get; set; }
    public List<ConversationSession> Sessions { get; set; }
}

public class GeoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string City { get; set; }
    public string District { get; set; }
}
```

### 3. Product (Polymorphic)

```csharp
public class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }  // ← Tenant isolation
    public string Name { get; set; }
    public string Description { get; set; }
    public ProductCategory Category { get; set; }
    public decimal BasePrice { get; set; }

    // Polymorphic metadata (JSON column)
    public string MetadataJson { get; set; }

    // RAG embeddings
    public Vector Embedding { get; set; }  // pgvector

    // Relationships
    public Tenant Tenant { get; set; }
    public List<ProductVariant> Variants { get; set; }
}

// Category-specific metadata classes
public class CosmeticsMetadata
{
    public List<string> Ingredients { get; set; }
    public List<string> SkinTypes { get; set; }
    public List<string> SkinConcerns { get; set; }
    public List<string> Contraindications { get; set; }
    public double? pH { get; set; }
    public string Texture { get; set; }
}

public class FashionMetadata
{
    public string Material { get; set; }
    public string Brand { get; set; }
    public List<string> Colors { get; set; }
    public List<string> Sizes { get; set; }
    public string Season { get; set; }
}

public class ElectronicsMetadata
{
    public string Brand { get; set; }
    public string Model { get; set; }
    public Dictionary<string, string> Specifications { get; set; }
    public int WarrantyMonths { get; set; }
}
```

### 4. BranchInventory (Stock per branch)

```csharp
public class BranchInventory
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }  // In carts
    public int AvailableQuantity => StockQuantity - ReservedQuantity;

    // Relationships
    public Branch Branch { get; set; }
    public ProductVariant ProductVariant { get; set; }
}
```

### 5. ConversationSession (Tenant + Branch aware)

```csharp
public class ConversationSession
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }  // ← Tenant isolation
    public Guid BranchId { get; set; }  // ← Branch context
    public string FacebookPSID { get; set; }
    public ConversationState State { get; set; }
    public string ContextJson { get; set; }

    // Skin profile (for cosmetics)
    public Guid? SkinProfileId { get; set; }
    public SkinProfile SkinProfile { get; set; }

    // Relationships
    public Tenant Tenant { get; set; }
    public Branch Branch { get; set; }
}
```

---

## Request Routing Flow

```
Facebook Webhook POST
    ↓
1. Extract PSID from webhook payload
    ↓
2. Lookup: PSID → Branch (via FacebookPageId mapping)
    ↓
3. Load: Branch → Tenant
    ↓
4. Set TenantContext (middleware)
    ↓
5. Load tenant-specific configuration
    ↓
6. Process message with tenant/branch context
    ↓
7. Query products filtered by TenantId
    ↓
8. Use branch-specific inventory
    ↓
9. Send reply via branch's PageAccessToken
```

### Routing Implementation

```csharp
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IBranchRepository _branchRepo;

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract Facebook Page ID from webhook
        var webhookEvent = await context.Request.ReadFromJsonAsync<WebhookEvent>();
        var pageId = webhookEvent?.Entry?.FirstOrDefault()?.Id;

        if (pageId != null)
        {
            // Resolve Branch → Tenant
            var branch = await _branchRepo.GetByFacebookPageIdAsync(pageId);
            if (branch == null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Branch not found");
                return;
            }

            // Set context for downstream services
            context.Items["TenantId"] = branch.TenantId;
            context.Items["BranchId"] = branch.Id;
            context.Items["Branch"] = branch;
        }

        await _next(context);
    }
}
```

---

## Database Strategy

### Option 1: Shared Schema + Row-Level Security (RECOMMENDED)

**Pros:**
- Cost-effective (1 database)
- Easy to manage
- Cross-tenant analytics possible

**Cons:**
- Risk of data leakage if queries miss TenantId filter
- Performance degradation at scale (millions of rows)

**Implementation:**
```sql
-- PostgreSQL Row-Level Security
ALTER TABLE products ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_policy ON products
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

-- Set tenant context per request
SET LOCAL app.current_tenant_id = 'tenant-uuid-here';
```

**EF Core:**
```csharp
public class MessengerBotDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Global query filter for tenant isolation
        modelBuilder.Entity<Product>()
            .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<ConversationSession>()
            .HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);

        // Apply to all tenant-scoped entities
    }
}
```

### Option 2: Database Per Tenant

**Pros:**
- Complete isolation
- Better performance per tenant
- Easier to backup/restore individual tenants

**Cons:**
- High operational overhead (100 tenants = 100 databases)
- Expensive
- Cross-tenant analytics impossible

**Not recommended** unless you have <10 tenants with strict compliance requirements.

---

## Multi-Category RAG Strategy

### Problem
Mỗi category cần:
- Embedding model khác nhau (cosmetics vs electronics có semantic space khác nhau)
- System prompt khác nhau
- Search strategy khác nhau

### Solution: Category Plugins

```csharp
public interface ICategoryPlugin
{
    ProductCategory Category { get; }
    string GetSystemPromptPath(Tenant tenant);
    string GetEmbeddingModel();
    Task<List<Product>> SearchAsync(string query, TenantContext context);
    object ParseMetadata(string metadataJson);
}

public class CosmeticsPlugin : ICategoryPlugin
{
    public ProductCategory Category => ProductCategory.Cosmetics;

    public string GetSystemPromptPath(Tenant tenant)
        => tenant.Configuration.SystemPromptPath
           ?? "Prompts/beauty-consultant-system-prompt.txt";

    public string GetEmbeddingModel() => "text-embedding-004";

    public async Task<List<Product>> SearchAsync(string query, TenantContext context)
    {
        // Extract skin profile from conversation
        var skinProfile = await ExtractSkinProfileAsync(query);

        // Semantic search with pgvector
        var embedding = await _embeddingService.GenerateAsync(query);

        // Filter by skin type compatibility
        return await _productRepo.SearchBySkinProfileAsync(
            embedding, skinProfile, context.TenantId);
    }

    public object ParseMetadata(string json)
        => JsonSerializer.Deserialize<CosmeticsMetadata>(json);
}

public class FashionPlugin : ICategoryPlugin
{
    public ProductCategory Category => ProductCategory.Fashion;

    public string GetSystemPromptPath(Tenant tenant)
        => "Prompts/fashion-consultant-system-prompt.txt";

    public string GetEmbeddingModel() => "text-embedding-004";

    public async Task<List<Product>> SearchAsync(string query, TenantContext context)
    {
        // Extract style preferences
        var preferences = await ExtractStylePreferencesAsync(query);

        // Semantic search + style matching
        var embedding = await _embeddingService.GenerateAsync(query);
        return await _productRepo.SearchByStyleAsync(
            embedding, preferences, context.TenantId);
    }

    public object ParseMetadata(string json)
        => JsonSerializer.Deserialize<FashionMetadata>(json);
}
```

### Plugin Registration

```csharp
// Program.cs
builder.Services.AddSingleton<ICategoryPlugin, CosmeticsPlugin>();
builder.Services.AddSingleton<ICategoryPlugin, FashionPlugin>();
builder.Services.AddSingleton<ICategoryPlugin, ElectronicsPlugin>();

builder.Services.AddSingleton<ICategoryPluginFactory, CategoryPluginFactory>();

// Factory
public class CategoryPluginFactory : ICategoryPluginFactory
{
    private readonly Dictionary<ProductCategory, ICategoryPlugin> _plugins;

    public CategoryPluginFactory(IEnumerable<ICategoryPlugin> plugins)
    {
        _plugins = plugins.ToDictionary(p => p.Category);
    }

    public ICategoryPlugin GetPlugin(ProductCategory category)
        => _plugins.TryGetValue(category, out var plugin)
            ? plugin
            : throw new NotSupportedException($"Category {category} not supported");
}
```

---

## Caching Strategy

### Problem
Global MemoryCache không phân biệt tenant → data leakage risk

### Solution: Tenant-Aware Distributed Cache

```csharp
public class TenantAwareCache : IDistributedCache
{
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var tenantKey = GetTenantKey(key);
        return await _cache.GetAsync(tenantKey, token);
    }

    public async Task SetAsync(string key, byte[] value,
        DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var tenantKey = GetTenantKey(key);
        await _cache.SetAsync(tenantKey, value, options, token);
    }

    private string GetTenantKey(string key)
        => $"tenant:{_tenantContext.TenantId}:{key}";
}

// Registration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
builder.Services.Decorate<IDistributedCache, TenantAwareCache>();
```

---

## Configuration Management

### Problem
Mỗi tenant/branch cần configuration riêng (API keys, prompts, etc.)

### Solution: Database-Backed Configuration

```csharp
public class TenantConfigurationProvider : IConfigurationProvider
{
    private readonly ITenantRepository _tenantRepo;
    private readonly ITenantContext _tenantContext;

    public bool TryGet(string key, out string value)
    {
        var tenant = _tenantRepo.GetByIdAsync(_tenantContext.TenantId).Result;
        if (tenant?.Configuration?.CustomSettings?.TryGetValue(key, out value) == true)
            return true;

        value = null;
        return false;
    }
}

// Usage
public class GeminiService
{
    public GeminiService(IConfiguration config, ITenantContext tenantContext)
    {
        // Automatically loads tenant-specific config
        var apiKey = config[$"Tenants:{tenantContext.TenantId}:Gemini:ApiKey"];
        var systemPrompt = config[$"Tenants:{tenantContext.TenantId}:SystemPromptPath"];
    }
}
```

---

## Security Considerations

### 1. Tenant Isolation
- **Database**: Row-level security + query filters
- **Cache**: Tenant-prefixed keys
- **Files**: Tenant-specific directories (`Prompts/{tenantId}/`)
- **Logs**: Include TenantId in all log entries

### 2. Branch Access Control
- Each branch has own PageAccessToken (encrypted at rest)
- Webhook signature validation per branch
- Rate limiting per branch (not global)

### 3. Data Encryption
```csharp
public class Branch
{
    [Encrypted]  // Custom attribute
    public string PageAccessToken { get; set; }
}

// EF Core value converter
public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IDataProtector protector)
        : base(
            v => protector.Protect(v),
            v => protector.Unprotect(v))
    { }
}
```

---

## Performance & Scaling

### Horizontal Scaling
```yaml
# Kubernetes deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: messenger-webhook
spec:
  replicas: 5  # Auto-scale based on CPU/memory
  template:
    spec:
      containers:
      - name: webhook
        image: messenger-webhook:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-secret
              key: connection-string
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: redis-secret
              key: connection-string
```

### Database Optimization
- **Indexes**: `(tenant_id, branch_id)` composite indexes on all tables
- **Partitioning**: Partition large tables by `tenant_id`
- **Connection pooling**: Per-tenant connection pools
- **Read replicas**: Route read queries to replicas

### Caching Strategy
- **L1 (Memory)**: Tenant configuration (5 min TTL)
- **L2 (Redis)**: Product catalog, session state (1 hour TTL)
- **L3 (Database)**: Source of truth

---

## Migration Path

### Phase 1: Foundation (3 months)
- [ ] Add Tenant, Branch entities
- [ ] Implement tenant resolution middleware
- [ ] Add TenantId to all existing entities
- [ ] Implement row-level security
- [ ] Migrate existing data to default tenant

### Phase 2: Multi-Branch (2 months)
- [ ] Implement branch routing
- [ ] Add BranchInventory
- [ ] Support multiple Facebook pages
- [ ] Branch-specific configuration

### Phase 3: Multi-Category (3 months)
- [ ] Design category plugin system
- [ ] Implement CosmeticsPlugin (existing logic)
- [ ] Add FashionPlugin
- [ ] Polymorphic product schema

### Phase 4: Scaling (2 months)
- [ ] Distributed caching (Redis)
- [ ] Database partitioning
- [ ] Kubernetes deployment
- [ ] Auto-scaling setup

### Phase 5: Admin Portal (2 months)
- [ ] Tenant management UI
- [ ] Branch management UI
- [ ] Product catalog management
- [ ] Analytics dashboard

---

## Risks & Mitigation

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Data leakage between tenants | Critical | Medium | Row-level security + automated tests |
| Performance degradation | High | High | Caching + database optimization |
| Configuration complexity | Medium | High | Admin portal + validation |
| Migration downtime | High | Low | Blue-green deployment |
| Cost overrun | Medium | Medium | Phased approach + MVP validation |

---

## Cost Estimate

### Infrastructure (per month)
- **Database**: PostgreSQL (100GB) - $200
- **Cache**: Redis (10GB) - $100
- **Compute**: 5 instances (2 vCPU, 4GB RAM) - $500
- **Storage**: S3 for images/files - $50
- **Total**: ~$850/month for 100 tenants

### Development (one-time)
- **Phase 1-5**: 12 months × 3 developers × $5000 = $180,000
- **QA/Testing**: 2 months × 1 QA × $4000 = $8,000
- **DevOps**: 1 month × 1 DevOps × $6000 = $6,000
- **Total**: ~$194,000

---

## Decision Points

### 1. Database Strategy
- [ ] **Option A**: Shared schema + RLS (recommended)
- [ ] **Option B**: Database per tenant

### 2. Deployment Model
- [ ] **Option A**: Kubernetes (recommended for scale)
- [ ] **Option B**: Docker Compose (simpler, limited scale)

### 3. Caching
- [ ] **Option A**: Redis (recommended)
- [ ] **Option B**: In-memory (not suitable for multi-instance)

### 4. Admin Portal
- [ ] **Option A**: Build custom (full control)
- [ ] **Option B**: Use existing admin framework (faster)

---

## Next Steps

1. **Review & Approve**: User reviews this proposal
2. **POC**: Build proof-of-concept (2 weeks)
   - Single tenant with 2 branches
   - Test routing logic
   - Validate performance
3. **Detailed Planning**: Break down into sprints
4. **Team Hiring**: Recruit 2-3 additional developers
5. **Start Phase 1**: Foundation work

---

## Questions for User

1. Timeline có chấp nhận được không? (12-18 tháng)
2. Budget có đủ không? (~$200K development + $850/month infrastructure)
3. Có team để maintain hệ thống phức tạp này không?
4. Có cần POC trước khi commit full redesign không?
5. Priority: Multi-tenant > Multi-branch > Multi-category đúng không?
