# Architecture Decision Records (ADR)

**Project**: Multi-Tenant Messenger Chatbot Platform
**Last Updated**: 2026-03-21

---

## ADR-001: Multi-Tenancy Pattern

### Context
Cần hỗ trợ nhiều cửa hàng độc lập trên cùng 1 hệ thống. Mỗi tenant cần:
- Data isolation hoàn toàn
- Configuration riêng (API keys, prompts)
- Performance không ảnh hưởng lẫn nhau
- Cost-effective để scale

### Options

#### Option A: Shared Schema + Row-Level Security ⭐ RECOMMENDED

**Architecture:**
```sql
-- Mọi table có tenant_id
CREATE TABLE products (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,  -- ← Tenant isolation
    name TEXT,
    ...
);

-- PostgreSQL Row-Level Security
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON products
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);
```

**Pros:**
- ✅ Cost-effective: 1 database cho tất cả tenants
- ✅ Easy maintenance: 1 schema, 1 migration path
- ✅ Cross-tenant analytics: Có thể query across tenants
- ✅ Resource sharing: Connection pooling, cache sharing
- ✅ Fast tenant provisioning: Chỉ cần INSERT vào `tenants` table

**Cons:**
- ❌ Data leakage risk: Nếu quên filter `tenant_id` → leak data
- ❌ Noisy neighbor: 1 tenant query chậm → ảnh hưởng tất cả
- ❌ Compliance: Một số regulations yêu cầu physical separation
- ❌ Backup/restore: Không thể backup 1 tenant riêng lẻ

**Implementation:**
```csharp
// EF Core global query filter
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>()
        .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);

    // Index for performance
    modelBuilder.Entity<Product>()
        .HasIndex(p => new { p.TenantId, p.Category });
}
```

**When to use:**
- 10-1000 tenants
- Tenants có usage pattern tương tự
- Cost là priority
- Không có strict compliance requirements

---

#### Option B: Database Per Tenant

**Architecture:**
```
tenant_001_db (Cửa hàng A)
tenant_002_db (Cửa hàng B)
tenant_003_db (Cửa hàng C)
```

**Pros:**
- ✅ Complete isolation: Không có data leakage risk
- ✅ Performance isolation: 1 tenant không ảnh hưởng khác
- ✅ Compliance-friendly: Physical separation
- ✅ Backup/restore per tenant: Dễ dàng
- ✅ Custom schema per tenant: Nếu cần

**Cons:**
- ❌ High cost: N tenants = N databases
- ❌ Operational overhead: Migrations phải chạy N lần
- ❌ No cross-tenant analytics: Không query được across tenants
- ❌ Resource waste: Mỗi DB cần minimum resources
- ❌ Slow provisioning: Tạo tenant mới = tạo database mới

**Implementation:**
```csharp
public class TenantDbContextFactory
{
    public MessengerBotDbContext CreateDbContext(Guid tenantId)
    {
        var connectionString = $"Host=localhost;Database=tenant_{tenantId};...";
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MessengerBotDbContext(options);
    }
}
```

**When to use:**
- <10 tenants (enterprise customers)
- Strict compliance requirements (HIPAA, PCI-DSS)
- Tenants có usage pattern rất khác nhau
- Budget không phải vấn đề

---

#### Option C: Schema Per Tenant (PostgreSQL Schemas)

**Architecture:**
```sql
-- Mỗi tenant 1 schema trong cùng database
CREATE SCHEMA tenant_001;
CREATE SCHEMA tenant_002;

-- Tables trong mỗi schema
CREATE TABLE tenant_001.products (...);
CREATE TABLE tenant_002.products (...);
```

**Pros:**
- ✅ Better isolation than shared schema
- ✅ Cheaper than database per tenant
- ✅ Backup per schema possible
- ✅ Can use search_path for routing

**Cons:**
- ❌ Still shares connection pool
- ❌ Migrations complex (N schemas)
- ❌ PostgreSQL-specific (not portable)
- ❌ Limited to ~1000 schemas per database

**When to use:**
- Middle ground giữa Option A và B
- PostgreSQL committed
- 10-100 tenants

---

### Decision: Option A (Shared Schema + RLS)

**Rationale:**
- Dự kiến 10-100 tenants trong 2 năm đầu
- Cost-effectiveness quan trọng cho startup
- Không có strict compliance requirements
- EF Core hỗ trợ tốt global query filters
- PostgreSQL RLS là battle-tested

**Mitigation for risks:**
- Automated tests: Verify tenant isolation trong mọi query
- Audit logging: Log mọi query với `tenant_id`
- Monitoring: Alert nếu query không có `tenant_id` filter
- Code review: Checklist cho mọi repository method

---

## ADR-002: Request Routing Strategy

### Context
Mỗi branch có Facebook Page riêng. Cần route webhook request → đúng tenant/branch.

### Options

#### Option A: PageId → Branch Lookup ⭐ RECOMMENDED

**Flow:**
```
Webhook POST
  ↓
Extract PageId from webhook.entry[0].id
  ↓
SELECT * FROM branches WHERE facebook_page_id = :pageId
  ↓
Load tenant_id from branch
  ↓
Set TenantContext
  ↓
Process message
```

**Pros:**
- ✅ Simple: 1 database lookup
- ✅ Fast: Index on `facebook_page_id`
- ✅ Reliable: PageId là unique identifier từ Facebook

**Cons:**
- ❌ Database dependency: Mọi request cần DB lookup
- ❌ Single point of failure: DB down → không route được

**Implementation:**
```csharp
public class TenantResolutionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var webhook = await context.Request.ReadFromJsonAsync<WebhookEvent>();
        var pageId = webhook?.Entry?.FirstOrDefault()?.Id;

        var branch = await _cache.GetOrCreateAsync($"page:{pageId}", async entry =>
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

**Caching strategy:**
- L1 (Memory): 5 min TTL
- L2 (Redis): 1 hour TTL
- Cache invalidation: Khi branch configuration thay đổi

---

#### Option B: Subdomain Routing

**Flow:**
```
tenant1.chatbot.com/webhook → Tenant 1
tenant2.chatbot.com/webhook → Tenant 2
```

**Pros:**
- ✅ No database lookup
- ✅ Clear separation
- ✅ Easy to route at load balancer level

**Cons:**
- ❌ Requires DNS setup per tenant
- ❌ Facebook webhook URL phải unique per branch
- ❌ SSL certificate management complex

**Not suitable** vì Facebook webhook URL phải register trước, không thể dynamic.

---

### Decision: Option A (PageId Lookup + Caching)

**Rationale:**
- Facebook PageId là stable identifier
- Caching giảm DB load xuống <1% requests
- Simple implementation
- No DNS/SSL complexity

---

## ADR-003: Product Schema Design

### Context
Cần hỗ trợ nhiều categories: cosmetics, fashion, electronics. Mỗi category có fields khác nhau.

### Options

#### Option A: Polymorphic JSON Column ⭐ RECOMMENDED

**Schema:**
```csharp
public class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public ProductCategory Category { get; set; }

    // Polymorphic metadata
    [Column(TypeName = "jsonb")]
    public string MetadataJson { get; set; }

    // RAG embedding
    public Vector Embedding { get; set; }
}

// Category-specific metadata
public class CosmeticsMetadata
{
    public List<string> Ingredients { get; set; }
    public List<string> SkinTypes { get; set; }
    public double? pH { get; set; }
}

public class FashionMetadata
{
    public string Material { get; set; }
    public List<string> Sizes { get; set; }
}
```

**Pros:**
- ✅ Flexible: Thêm category mới không cần migration
- ✅ Type-safe: Deserialize to strongly-typed classes
- ✅ PostgreSQL JSONB: Có thể index và query
- ✅ Simple schema: 1 products table

**Cons:**
- ❌ Validation phức tạp: Phải validate JSON structure
- ❌ Queries khó hơn: JSON queries không intuitive
- ❌ No foreign keys: Không enforce referential integrity

**Implementation:**
```csharp
// Serialize/deserialize based on category
public T GetMetadata<T>() where T : class
{
    return JsonSerializer.Deserialize<T>(MetadataJson);
}

public void SetMetadata<T>(T metadata) where T : class
{
    MetadataJson = JsonSerializer.Serialize(metadata);
}

// Query example
var cosmeticsProducts = await _context.Products
    .Where(p => p.Category == ProductCategory.Cosmetics)
    .Where(p => EF.Functions.JsonContains(
        p.MetadataJson,
        JsonSerializer.Serialize(new { SkinTypes = new[] { "oily" } })))
    .ToListAsync();
```

---

#### Option B: Table Per Category

**Schema:**
```sql
CREATE TABLE products (
    id UUID PRIMARY KEY,
    tenant_id UUID,
    name TEXT,
    category TEXT
);

CREATE TABLE cosmetics_products (
    product_id UUID REFERENCES products(id),
    ingredients TEXT[],
    skin_types TEXT[],
    ph DECIMAL
);

CREATE TABLE fashion_products (
    product_id UUID REFERENCES products(id),
    material TEXT,
    sizes TEXT[]
);
```

**Pros:**
- ✅ Type-safe: Proper columns với constraints
- ✅ Easy queries: Standard SQL
- ✅ Foreign keys: Referential integrity

**Cons:**
- ❌ Rigid: Thêm category = new table + migration
- ❌ Complex joins: Phải join nhiều tables
- ❌ Code duplication: Repository per category

**Not recommended** vì quá rigid cho multi-category platform.

---

#### Option C: EAV (Entity-Attribute-Value)

**Schema:**
```sql
CREATE TABLE products (id, tenant_id, name, category);
CREATE TABLE product_attributes (
    product_id UUID,
    attribute_name TEXT,
    attribute_value TEXT
);
```

**Pros:**
- ✅ Maximum flexibility

**Cons:**
- ❌ Performance nightmare: Queries rất chậm
- ❌ No type safety
- ❌ Complex queries

**NEVER use EAV** - anti-pattern.

---

### Decision: Option A (Polymorphic JSON)

**Rationale:**
- Flexibility quan trọng cho multi-category
- PostgreSQL JSONB performance tốt
- Type-safe với C# classes
- Dễ thêm category mới

**Validation strategy:**
```csharp
public interface ICategoryMetadataValidator
{
    bool Validate(string metadataJson, out List<string> errors);
}

public class CosmeticsMetadataValidator : ICategoryMetadataValidator
{
    public bool Validate(string json, out List<string> errors)
    {
        errors = new List<string>();
        var metadata = JsonSerializer.Deserialize<CosmeticsMetadata>(json);

        if (metadata.Ingredients == null || !metadata.Ingredients.Any())
            errors.Add("Ingredients required");

        if (metadata.pH.HasValue && (metadata.pH < 0 || metadata.pH > 14))
            errors.Add("pH must be 0-14");

        return !errors.Any();
    }
}
```

---

## ADR-004: Caching Strategy

### Context
Multi-tenant system cần caching để giảm DB load, nhưng phải đảm bảo tenant isolation.

### Options

#### Option A: Tenant-Aware Distributed Cache ⭐ RECOMMENDED

**Architecture:**
```
Redis Cluster
├── tenant:001:product:abc123
├── tenant:001:session:psid456
├── tenant:002:product:def789
└── tenant:002:session:psid012
```

**Implementation:**
```csharp
public class TenantAwareCache : IDistributedCache
{
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var tenantKey = $"tenant:{_tenantContext.TenantId}:{key}";
        return await _cache.GetAsync(tenantKey, token);
    }

    public async Task SetAsync(string key, byte[] value,
        DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var tenantKey = $"tenant:{_tenantContext.TenantId}:{key}";
        await _cache.SetAsync(tenantKey, value, options, token);
    }
}
```

**Pros:**
- ✅ Tenant isolation: Keys có tenant prefix
- ✅ Distributed: Scale across instances
- ✅ Fast: Redis performance
- ✅ Eviction per tenant: Có thể clear cache 1 tenant

**Cons:**
- ❌ Infrastructure cost: Redis cluster
- ❌ Network latency: Remote cache
- ❌ Complexity: Cache invalidation

---

#### Option B: In-Memory Cache Per Instance

**Pros:**
- ✅ Fast: No network latency
- ✅ Simple: No infrastructure

**Cons:**
- ❌ Not distributed: Mỗi instance có cache riêng
- ❌ Memory pressure: Limited by instance RAM
- ❌ Cache inconsistency: Invalidation khó

**Not suitable** cho multi-instance deployment.

---

### Decision: Option A (Redis + Tenant-Aware Keys)

**Caching layers:**
```
L1: In-Memory (per instance)
    ├── Tenant configuration (5 min TTL)
    └── Branch routing (5 min TTL)

L2: Redis (distributed)
    ├── Product catalog (1 hour TTL)
    ├── Session state (30 min TTL)
    └── RAG embeddings (24 hour TTL)

L3: PostgreSQL (source of truth)
```

**Cache invalidation:**
- Write-through: Update DB + invalidate cache
- TTL-based: Automatic expiration
- Event-based: Pub/sub for real-time invalidation

---

## ADR-005: RAG Strategy for Multi-Category

### Context
Mỗi category cần semantic search khác nhau. Cosmetics search theo ingredients/skin-type, fashion search theo style/material.

### Options

#### Option A: Category Plugin System ⭐ RECOMMENDED

**Architecture:**
```csharp
public interface ICategoryPlugin
{
    ProductCategory Category { get; }
    string GetSystemPromptPath(Tenant tenant);
    string GetEmbeddingModel();
    Task<List<Product>> SearchAsync(string query, TenantContext context);
}

public class CosmeticsPlugin : ICategoryPlugin
{
    public async Task<List<Product>> SearchAsync(string query, TenantContext ctx)
    {
        // 1. Extract skin profile from query
        var skinProfile = await _nlpService.ExtractSkinProfileAsync(query);

        // 2. Generate embedding
        var embedding = await _embeddingService.GenerateAsync(query);

        // 3. Vector search + skin type filter
        return await _db.Products
            .Where(p => p.TenantId == ctx.TenantId)
            .Where(p => p.Category == ProductCategory.Cosmetics)
            .OrderBy(p => p.Embedding.CosineDistance(embedding))
            .Where(p => EF.Functions.JsonContains(
                p.MetadataJson,
                JsonSerializer.Serialize(new { SkinTypes = skinProfile.SkinType })))
            .Take(10)
            .ToListAsync();
    }
}
```

**Pros:**
- ✅ Extensible: Thêm category = implement plugin
- ✅ Isolated: Mỗi category có logic riêng
- ✅ Testable: Unit test per plugin
- ✅ Flexible: Mỗi category có embedding model riêng

**Cons:**
- ❌ Complexity: Nhiều plugins để maintain
- ❌ Consistency: Khó đảm bảo UX consistent

---

#### Option B: Single RAG Model for All Categories

**Pros:**
- ✅ Simple: 1 model, 1 logic

**Cons:**
- ❌ Poor accuracy: Cosmetics và electronics có semantic space khác nhau
- ❌ Not scalable: Thêm category → retrain model

**Not suitable** cho multi-category.

---

### Decision: Option A (Category Plugins)

**Plugin registration:**
```csharp
// Program.cs
builder.Services.AddSingleton<ICategoryPlugin, CosmeticsPlugin>();
builder.Services.AddSingleton<ICategoryPlugin, FashionPlugin>();
builder.Services.AddSingleton<ICategoryPlugin, ElectronicsPlugin>();

builder.Services.AddSingleton<ICategoryPluginFactory>(sp =>
{
    var plugins = sp.GetServices<ICategoryPlugin>();
    return new CategoryPluginFactory(plugins);
});
```

**Usage:**
```csharp
var plugin = _pluginFactory.GetPlugin(product.Category);
var results = await plugin.SearchAsync(userQuery, tenantContext);
```

---

## ADR-006: Security & Compliance

### Tenant Isolation Enforcement

**1. Database Level:**
```sql
-- Row-Level Security
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON products
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

-- Set tenant context per request
SET LOCAL app.current_tenant_id = 'uuid-here';
```

**2. Application Level:**
```csharp
// Global query filter
modelBuilder.Entity<Product>()
    .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);

// Automated tests
[Fact]
public async Task ProductRepository_ShouldNotLeakDataBetweenTenants()
{
    // Arrange
    var tenant1 = CreateTenant();
    var tenant2 = CreateTenant();
    var product1 = CreateProduct(tenant1.Id);
    var product2 = CreateProduct(tenant2.Id);

    // Act
    SetTenantContext(tenant1.Id);
    var results = await _productRepo.GetAllAsync();

    // Assert
    Assert.Contains(product1, results);
    Assert.DoesNotContain(product2, results);
}
```

**3. Cache Level:**
```csharp
// Tenant-prefixed keys
var key = $"tenant:{tenantId}:product:{productId}";
```

**4. Logging Level:**
```csharp
// Always log tenant_id
_logger.LogInformation(
    "Product created: {ProductId} for tenant {TenantId}",
    product.Id, tenantContext.TenantId);
```

### Data Encryption

**At Rest:**
```csharp
// Encrypt sensitive fields
public class Branch
{
    [Encrypted]  // Custom attribute
    public string PageAccessToken { get; set; }

    [Encrypted]
    public string WebhookVerifyToken { get; set; }
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

**In Transit:**
- HTTPS only
- TLS 1.3
- Certificate pinning for external APIs

---

## Summary: Recommended Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Load Balancer                        │
└─────────────────────────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
   ┌─────────┐       ┌─────────┐       ┌─────────┐
   │ App     │       │ App     │       │ App     │
   │ Instance│       │ Instance│       │ Instance│
   │    1    │       │    2    │       │    3    │
   └─────────┘       └─────────┘       └─────────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
   ┌─────────┐       ┌─────────┐       ┌─────────┐
   │ Redis   │       │ Postgres│       │ pgvector│
   │ Cache   │       │ (RLS)   │       │ (RAG)   │
   └─────────┘       └─────────┘       └─────────┘
```

**Key Decisions:**
1. ✅ Shared Schema + Row-Level Security
2. ✅ PageId → Branch Lookup (cached)
3. ✅ Polymorphic JSON for product metadata
4. ✅ Redis distributed cache với tenant-aware keys
5. ✅ Category plugin system cho RAG
6. ✅ Multi-layer security (DB + App + Cache + Logs)

**Trade-offs Accepted:**
- Data leakage risk → Mitigated by automated tests + RLS
- Noisy neighbor → Mitigated by caching + query optimization
- Complexity → Justified by flexibility + cost savings

**Next Steps:**
1. Build POC (2 weeks) để validate
2. Load testing với 100 tenants
3. Security audit
4. Start Phase 1 implementation

---

## ADR-007: Software Architecture for Phase 8 (Multi-Tenant Scale)

### Context

**Current State (MVP - Phase 1-7):**
- Simple Layered Architecture (3-tier)
- 1 cửa hàng, 1 fanpage, cosmetics only
- Team size: 1 developer
- Timeline: 12 weeks

**Phase 8 Requirements:**
- Multi-tenant (100+ stores)
- Multi-branch (mỗi store nhiều chi nhánh)
- Multi-category (cosmetics, fashion, electronics)
- Timeline: 10 months
- Team size: 3-5 developers

**Problem:**
Layered Architecture đơn giản không đủ cho complexity của Phase 8. Cần architecture mạnh hơn để:
- Manage complex domain logic (tenant isolation, category plugins, branch routing)
- Enable parallel development (3-5 devs)
- Maintain testability và maintainability
- Support long-term evolution (2+ years)

---

### Current Architecture (MVP)

**Layered Architecture (3-tier):**
```
┌─────────────────────────────────────┐
│   Presentation Layer (API)          │
│   - Endpoints                       │
│   - Middleware                      │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│   Service Layer (Business Logic)    │
│   - GeminiService                   │
│   - MessengerService                │
│   - ProductService                  │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│   Data Layer (Repositories)         │
│   - ProductRepository               │
│   - SessionRepository               │
│   - EF Core DbContext               │
└─────────────────────────────────────┘
```

**Pros:**
- ✅ Simple, dễ hiểu
- ✅ Nhanh implement (12 weeks MVP)
- ✅ Phù hợp với 1 developer

**Cons:**
- ❌ Business logic rải rác trong Services
- ❌ Khó test (tight coupling với infrastructure)
- ❌ Không có domain model rõ ràng
- ❌ Khó parallel development

---

### Proposed Architecture: Clean Architecture + DDD

**Clean Architecture Layers:**
```
┌───────────────────────────────────────────────────────┐
│                  Presentation Layer                   │
│  - API Controllers                                    │
│  - Middleware (TenantResolution, Auth)                │
│  - DTOs, Mappers                                      │
└───────────────────────────────────────────────────────┘
                        ↓
┌───────────────────────────────────────────────────────┐
│                 Application Layer                     │
│  - Use Cases (Commands/Queries)                       │
│  - Application Services                               │
│  - Interfaces (Ports)                                 │
└───────────────────────────────────────────────────────┘
                        ↓
┌───────────────────────────────────────────────────────┐
│                   Domain Layer                        │
│  - Entities (Tenant, Branch, Product)                 │
│  - Value Objects (SkinProfile, Ingredients)           │
│  - Domain Services                                    │
│  - Domain Events                                      │
│  - Aggregates                                         │
└───────────────────────────────────────────────────────┘
                        ↑
┌───────────────────────────────────────────────────────┐
│               Infrastructure Layer                    │
│  - Repositories (EF Core)                             │
│  - External Services (Gemini, Facebook)               │
│  - Caching (Redis)                                    │
│  - RAG (pgvector)                                     │
└───────────────────────────────────────────────────────┘
```

**Dependency Rule:**
- Domain Layer: Không depend vào gì (pure business logic)
- Application Layer: Depend vào Domain
- Infrastructure Layer: Depend vào Domain + Application (implement interfaces)
- Presentation Layer: Depend vào Application

---

### DDD Strategic Design

**Bounded Contexts:**

```
┌─────────────────────────────────────────────────────┐
│          Tenant Management Context                  │
│  - Tenant, Branch entities                          │
│  - Tenant configuration                             │
│  - Branch routing                                   │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│          Product Catalog Context                    │
│  - Product, Variant entities                        │
│  - Category plugins                                 │
│  - RAG search                                       │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│          Consultation Context                       │
│  - ConversationSession                              │
│  - SkinProfile                                      │
│  - Gemini integration                               │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│          Order Management Context                   │
│  - Cart, Order entities                             │
│  - Payment integration                              │
│  - Order workflow                                   │
└─────────────────────────────────────────────────────┘
```

**Context Mapping:**
- Tenant Management → Product Catalog: Shared Kernel (TenantId)
- Product Catalog → Consultation: Customer/Supplier (products cho consultation)
- Consultation → Order Management: Customer/Supplier (cart từ consultation)

---

### Domain Model Example

**Aggregate Root: Product**
```csharp
namespace Domain.ProductCatalog
{
    public class Product : AggregateRoot<ProductId>
    {
        public TenantId TenantId { get; private set; }
        public ProductName Name { get; private set; }
        public ProductCategory Category { get; private set; }
        public Money BasePrice { get; private set; }

        // Value Objects
        public CosmeticsMetadata Metadata { get; private set; }
        public Embedding Embedding { get; private set; }

        // Collections
        private readonly List<ProductVariant> _variants = new();
        public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

        // Domain Methods
        public void UpdateMetadata(CosmeticsMetadata metadata)
        {
            // Business rules
            if (Category != ProductCategory.Cosmetics)
                throw new DomainException("Only cosmetics can have CosmeticsMetadata");

            Metadata = metadata;
            AddDomainEvent(new ProductMetadataUpdatedEvent(Id, metadata));
        }

        public void AddVariant(ProductVariant variant)
        {
            // Business rules
            if (_variants.Any(v => v.SKU == variant.SKU))
                throw new DomainException("Duplicate SKU");

            _variants.Add(variant);
            AddDomainEvent(new ProductVariantAddedEvent(Id, variant.Id));
        }
    }
}
```

**Value Object: SkinProfile**
```csharp
namespace Domain.Consultation
{
    public class SkinProfile : ValueObject
    {
        public SkinType Type { get; private set; }
        public IReadOnlyList<SkinConcern> Concerns { get; private set; }
        public IReadOnlyList<Ingredient> Sensitivities { get; private set; }

        public SkinProfile(SkinType type, List<SkinConcern> concerns, List<Ingredient> sensitivities)
        {
            // Validation
            if (concerns == null || !concerns.Any())
                throw new DomainException("At least one concern required");

            Type = type;
            Concerns = concerns.AsReadOnly();
            Sensitivities = sensitivities?.AsReadOnly() ?? new List<Ingredient>().AsReadOnly();
        }

        public bool IsCompatibleWith(Product product)
        {
            var metadata = product.Metadata as CosmeticsMetadata;
            if (metadata == null) return false;

            // Business logic
            return metadata.SkinTypes.Contains(Type) &&
                   !metadata.Ingredients.Any(i => Sensitivities.Contains(i));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Type;
            foreach (var concern in Concerns) yield return concern;
            foreach (var sensitivity in Sensitivities) yield return sensitivity;
        }
    }
}
```

---

### Application Layer: Use Cases

**CQRS Pattern:**

**Command:**
```csharp
namespace Application.ProductCatalog.Commands
{
    public record CreateProductCommand(
        TenantId TenantId,
        string Name,
        ProductCategory Category,
        decimal BasePrice,
        CosmeticsMetadataDto Metadata
    ) : ICommand<ProductId>;

    public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, ProductId>
    {
        private readonly IProductRepository _productRepo;
        private readonly IEmbeddingService _embeddingService;
        private readonly IUnitOfWork _unitOfWork;

        public async Task<ProductId> Handle(CreateProductCommand command, CancellationToken ct)
        {
            // 1. Create domain entity
            var product = Product.Create(
                command.TenantId,
                new ProductName(command.Name),
                command.Category,
                new Money(command.BasePrice)
            );

            // 2. Set metadata
            var metadata = CosmeticsMetadata.FromDto(command.Metadata);
            product.UpdateMetadata(metadata);

            // 3. Generate embedding
            var embeddingText = BuildEmbeddingText(product, metadata);
            var embedding = await _embeddingService.GenerateAsync(embeddingText, ct);
            product.SetEmbedding(new Embedding(embedding));

            // 4. Save
            await _productRepo.AddAsync(product, ct);
            await _unitOfWork.CommitAsync(ct);

            return product.Id;
        }
    }
}
```

**Query:**
```csharp
namespace Application.ProductCatalog.Queries
{
    public record SearchProductsQuery(
        TenantId TenantId,
        string Query,
        SkinProfile? SkinProfile,
        int Limit
    ) : IQuery<List<ProductDto>>;

    public class SearchProductsQueryHandler : IQueryHandler<SearchProductsQuery, List<ProductDto>>
    {
        private readonly IProductReadRepository _productReadRepo;
        private readonly IEmbeddingService _embeddingService;

        public async Task<List<ProductDto>> Handle(SearchProductsQuery query, CancellationToken ct)
        {
            // 1. Generate query embedding
            var embedding = await _embeddingService.GenerateAsync(query.Query, ct);

            // 2. Vector search
            var products = await _productReadRepo.SearchBySimilarityAsync(
                query.TenantId,
                embedding,
                query.Limit,
                ct
            );

            // 3. Filter by skin profile
            if (query.SkinProfile != null)
            {
                products = products
                    .Where(p => query.SkinProfile.IsCompatibleWith(p))
                    .ToList();
            }

            // 4. Map to DTOs
            return products.Select(ProductDto.FromDomain).ToList();
        }
    }
}
```

---

### Infrastructure Layer: Repository Implementation

```csharp
namespace Infrastructure.ProductCatalog
{
    public class ProductRepository : IProductRepository
    {
        private readonly MessengerBotDbContext _context;
        private readonly ITenantContext _tenantContext;

        public async Task<Product?> GetByIdAsync(ProductId id, CancellationToken ct)
        {
            var entity = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == id.Value, ct);

            return entity == null ? null : ToDomain(entity);
        }

        public async Task AddAsync(Product product, CancellationToken ct)
        {
            var entity = ToEntity(product);
            await _context.Products.AddAsync(entity, ct);
        }

        // Mapping: Domain ↔ Entity
        private Product ToDomain(ProductEntity entity)
        {
            var product = Product.Reconstitute(
                new ProductId(entity.Id),
                new TenantId(entity.TenantId),
                new ProductName(entity.Name),
                entity.Category,
                new Money(entity.BasePrice)
            );

            // Reconstitute metadata, variants, etc.
            return product;
        }

        private ProductEntity ToEntity(Product product)
        {
            return new ProductEntity
            {
                Id = product.Id.Value,
                TenantId = product.TenantId.Value,
                Name = product.Name.Value,
                Category = product.Category,
                BasePrice = product.BasePrice.Amount,
                // Map metadata, variants, etc.
            };
        }
    }
}
```

---

### Benefits

**1. Testability**
```csharp
// Unit test domain logic (no infrastructure)
[Fact]
public void Product_UpdateMetadata_ShouldThrow_WhenNotCosmetics()
{
    var product = Product.Create(
        tenantId, name, ProductCategory.Fashion, price);

    var metadata = new CosmeticsMetadata(...);

    Assert.Throws<DomainException>(() =>
        product.UpdateMetadata(metadata));
}

// Integration test use case (with test doubles)
[Fact]
public async Task CreateProduct_ShouldGenerateEmbedding()
{
    var mockEmbedding = new Mock<IEmbeddingService>();
    var handler = new CreateProductCommandHandler(
        _productRepo, mockEmbedding.Object, _unitOfWork);

    var command = new CreateProductCommand(...);
    var productId = await handler.Handle(command, CancellationToken.None);

    mockEmbedding.Verify(e => e.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
}
```

**2. Parallel Development**
- Team A: Tenant Management Context
- Team B: Product Catalog Context
- Team C: Consultation Context
- Team D: Order Management Context

→ Minimal conflicts vì bounded contexts độc lập

**3. Domain Clarity**
- Business rules rõ ràng trong Domain Layer
- Dễ onboard developers mới
- Domain experts có thể review domain code

**4. Maintainability**
- Changes isolated trong bounded contexts
- Easy to refactor (domain không depend infrastructure)
- Clear separation of concerns

---

### Trade-offs

**Cons:**
- ❌ More complex than Layered Architecture
- ❌ More boilerplate (DTOs, mappers, interfaces)
- ❌ Steeper learning curve
- ❌ Longer initial setup time

**Mitigation:**
- Training cho team về Clean Arch + DDD
- Code generators cho boilerplate
- Clear documentation và examples
- Gradual migration (không big bang)

---

### Migration Strategy (Phase 7 → Phase 8)

**Step 1: Introduce Domain Layer (2 weeks)**
- Create Domain project
- Extract entities từ Data layer
- Add value objects, domain services
- Keep existing Services layer hoạt động

**Step 2: Introduce Application Layer (3 weeks)**
- Create Application project
- Implement Commands/Queries cho core flows
- Migrate Services logic vào Use Cases
- Dual-run: old Services + new Use Cases

**Step 3: Refactor Infrastructure (2 weeks)**
- Implement Repository interfaces từ Domain
- Move EF Core entities sang Infrastructure
- Implement adapters cho external services

**Step 4: Update Presentation (1 week)**
- Controllers call Use Cases thay vì Services
- Remove old Services layer
- Update dependency injection

**Step 5: Bounded Contexts (2 months)**
- Split monolith thành bounded contexts
- Define context boundaries
- Implement context mapping

**Total Migration Time: 3 months** (parallel với Phase 8.1)

---

### Project Structure

```
src/
├── Domain/
│   ├── Common/
│   │   ├── AggregateRoot.cs
│   │   ├── Entity.cs
│   │   ├── ValueObject.cs
│   │   └── DomainEvent.cs
│   ├── TenantManagement/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Services/
│   │   └── Events/
│   ├── ProductCatalog/
│   ├── Consultation/
│   └── OrderManagement/
├── Application/
│   ├── Common/
│   │   ├── ICommand.cs
│   │   ├── IQuery.cs
│   │   └── IUnitOfWork.cs
│   ├── TenantManagement/
│   │   ├── Commands/
│   │   ├── Queries/
│   │   └── DTOs/
│   ├── ProductCatalog/
│   ├── Consultation/
│   └── OrderManagement/
├── Infrastructure/
│   ├── Persistence/
│   │   ├── MessengerBotDbContext.cs
│   │   ├── Repositories/
│   │   └── Configurations/
│   ├── ExternalServices/
│   │   ├── Gemini/
│   │   ├── Facebook/
│   │   └── RAG/
│   └── Caching/
└── Presentation/
    ├── API/
    │   ├── Controllers/
    │   ├── Middleware/
    │   └── DTOs/
    └── BackgroundServices/
```

---

### Decision: Clean Architecture + DDD for Phase 8

**Rationale:**
- Phase 8 complexity justifies investment
- Team size (3-5 devs) benefits from clear boundaries
- Long-term maintainability critical (2+ years)
- Testability essential cho multi-tenant system
- Domain complexity (tenant isolation, category plugins) fits DDD

**When to start:**
- After MVP complete (Phase 1-7)
- During Phase 8.1 (Multi-Branch)
- Gradual migration (3 months)

**Success Criteria:**
- ✅ Domain logic testable without infrastructure
- ✅ Bounded contexts enable parallel development
- ✅ New features added without touching other contexts
- ✅ Team velocity maintained after learning curve

---

### Advanced Patterns: CQRS & Event Sourcing

**When to apply:**

#### CQRS (Command Query Responsibility Segregation)

**Apply when:**
- Read/write patterns rất khác nhau
- Read queries phức tạp (joins nhiều tables, aggregations)
- Write operations cần strong consistency
- Cần optimize read performance độc lập với write

**Example: Product Search vs Product Management**

```csharp
// Write Model (Command Side)
public class Product : AggregateRoot<ProductId>
{
    // Full domain model với business rules
    public void UpdateMetadata(CosmeticsMetadata metadata) { ... }
    public void AddVariant(ProductVariant variant) { ... }
}

// Read Model (Query Side)
public class ProductSearchModel
{
    // Denormalized, optimized cho search
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string[] Ingredients { get; set; }
    public string[] SkinTypes { get; set; }
    public float[] Embedding { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

// Separate repositories
public interface IProductWriteRepository
{
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
}

public interface IProductReadRepository
{
    Task<ProductSearchModel> GetByIdAsync(Guid id);
    Task<List<ProductSearchModel>> SearchAsync(string query);
}
```

**Benefits:**
- ✅ Read queries không bị ảnh hưởng bởi write locks
- ✅ Có thể scale read/write independently
- ✅ Read models optimized cho specific queries
- ✅ Write model focus vào business rules

**Trade-offs:**
- ❌ Eventual consistency giữa read/write models
- ❌ Complexity tăng (2 models, synchronization)
- ❌ Cần event handlers để sync read models

**Implementation:**

```csharp
// Command Handler (Write)
public class CreateProductCommandHandler
{
    private readonly IProductWriteRepository _writeRepo;
    private readonly IEventBus _eventBus;

    public async Task<ProductId> Handle(CreateProductCommand cmd)
    {
        var product = Product.Create(...);
        await _writeRepo.AddAsync(product);

        // Publish event
        await _eventBus.PublishAsync(new ProductCreatedEvent(product.Id, ...));

        return product.Id;
    }
}

// Event Handler (Update Read Model)
public class ProductCreatedEventHandler
{
    private readonly IProductReadRepository _readRepo;

    public async Task Handle(ProductCreatedEvent evt)
    {
        var readModel = new ProductSearchModel
        {
            Id = evt.ProductId,
            Name = evt.Name,
            Ingredients = evt.Ingredients,
            // ... denormalized data
        };

        await _readRepo.AddAsync(readModel);
    }
}
```

---

#### Event Sourcing

**Apply when:**
- Cần audit trail chi tiết (who, what, when, why)
- Cần replay events để rebuild state
- Cần temporal queries (state tại thời điểm X)
- Domain có nhiều state transitions quan trọng

**Example: Order Workflow với Event Sourcing**

```csharp
// Events (immutable facts)
public record OrderCreatedEvent(
    Guid OrderId,
    Guid TenantId,
    Guid CustomerId,
    List<OrderItemDto> Items,
    DateTime CreatedAt
);

public record OrderPaymentReceivedEvent(
    Guid OrderId,
    decimal Amount,
    string PaymentMethod,
    DateTime ReceivedAt
);

public record OrderShippedEvent(
    Guid OrderId,
    string TrackingNumber,
    DateTime ShippedAt
);

// Aggregate reconstituted from events
public class Order : AggregateRoot<OrderId>
{
    private OrderStatus _status;
    private decimal _totalAmount;
    private List<OrderItem> _items = new();

    // Apply events to rebuild state
    public void Apply(OrderCreatedEvent evt)
    {
        Id = new OrderId(evt.OrderId);
        _status = OrderStatus.Created;
        _items = evt.Items.Select(OrderItem.FromDto).ToList();
        _totalAmount = _items.Sum(i => i.Price * i.Quantity);
    }

    public void Apply(OrderPaymentReceivedEvent evt)
    {
        _status = OrderStatus.Paid;
    }

    public void Apply(OrderShippedEvent evt)
    {
        _status = OrderStatus.Shipped;
    }

    // Command methods produce events
    public void ReceivePayment(decimal amount, string method)
    {
        if (_status != OrderStatus.Created)
            throw new DomainException("Order already paid");

        if (amount != _totalAmount)
            throw new DomainException("Payment amount mismatch");

        AddDomainEvent(new OrderPaymentReceivedEvent(
            Id.Value, amount, method, DateTime.UtcNow));
    }
}
```

**Event Store:**

```csharp
public interface IEventStore
{
    Task AppendAsync(Guid aggregateId, IEnumerable<DomainEvent> events);
    Task<List<DomainEvent>> GetEventsAsync(Guid aggregateId);
}

public class EventStoreRepository : IOrderRepository
{
    private readonly IEventStore _eventStore;

    public async Task<Order> GetByIdAsync(OrderId id)
    {
        // Load events
        var events = await _eventStore.GetEventsAsync(id.Value);

        // Reconstitute aggregate
        var order = new Order();
        foreach (var evt in events)
        {
            order.Apply((dynamic)evt);
        }

        return order;
    }

    public async Task SaveAsync(Order order)
    {
        // Get uncommitted events
        var events = order.GetUncommittedEvents();

        // Append to event store
        await _eventStore.AppendAsync(order.Id.Value, events);

        order.ClearUncommittedEvents();
    }
}
```

**Benefits:**
- ✅ Complete audit trail (mọi thay đổi được record)
- ✅ Temporal queries: "Order status lúc 10:00 AM?"
- ✅ Event replay: Rebuild state từ events
- ✅ Debug dễ: Xem toàn bộ history

**Trade-offs:**
- ❌ Complexity cao (event store, event versioning)
- ❌ Query hiện tại phức tạp (phải replay events)
- ❌ Storage tăng (store mọi events)
- ❌ Event schema evolution khó

**Mitigation:**
- Snapshots: Lưu state tại checkpoint để tránh replay toàn bộ
- CQRS: Combine với CQRS cho read performance
- Event versioning: Upcasting cho backward compatibility
- **Outbox Pattern**: Đảm bảo atomicity giữa DB write và event publishing

```csharp
// Snapshot
public class OrderSnapshot
{
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public int EventVersion { get; set; }  // Last event applied
}

// Load with snapshot
public async Task<Order> GetByIdAsync(OrderId id)
{
    // Load snapshot
    var snapshot = await _snapshotStore.GetAsync(id.Value);

    var order = new Order();
    if (snapshot != null)
    {
        order.LoadFromSnapshot(snapshot);
    }

    // Load events after snapshot
    var events = await _eventStore.GetEventsAsync(
        id.Value,
        fromVersion: snapshot?.EventVersion ?? 0);

    foreach (var evt in events)
    {
        order.Apply((dynamic)evt);
    }

    return order;
}
```

---

### Outbox Pattern (Transactional Messaging)

**Problem:**
Khi save aggregate + publish events, có dual-write problem:
```csharp
// ❌ NOT ATOMIC - nếu PublishAsync fail, DB đã commit
await _orderRepo.SaveAsync(order);  // Write to DB
await _eventBus.PublishAsync(events);  // Publish events
```

Nếu `PublishAsync` fail → Order saved nhưng events không được publish → inconsistency.

**Solution: Outbox Pattern**

```csharp
// Outbox table
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; }  // JSON
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
}

// Save aggregate + events trong cùng transaction
public class EventStoreRepository : IOrderRepository
{
    private readonly MessengerBotDbContext _context;

    public async Task SaveAsync(Order order)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Save aggregate events to event store
            var events = order.GetUncommittedEvents();
            await _eventStore.AppendAsync(order.Id.Value, events);

            // 2. Save events to outbox (same transaction)
            foreach (var evt in events)
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = evt.GetType().Name,
                    EventData = JsonSerializer.Serialize(evt),
                    CreatedAt = DateTime.UtcNow
                };
                await _context.OutboxMessages.AddAsync(outboxMessage);
            }

            // 3. Commit transaction (atomic)
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            order.ClearUncommittedEvents();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

// Background service publish events từ outbox
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

                // Get unprocessed messages
                var messages = await context.OutboxMessages
                    .Where(m => m.ProcessedAt == null)
                    .Where(m => m.RetryCount < 5)
                    .OrderBy(m => m.CreatedAt)
                    .Take(100)
                    .ToListAsync(ct);

                foreach (var message in messages)
                {
                    try
                    {
                        // Deserialize event
                        var eventType = Type.GetType(message.EventType);
                        var evt = JsonSerializer.Deserialize(message.EventData, eventType);

                        // Publish event
                        await eventBus.PublishAsync((DomainEvent)evt, ct);

                        // Mark as processed
                        message.ProcessedAt = DateTime.UtcNow;
                        await context.SaveChangesAsync(ct);

                        _logger.LogInformation(
                            "Processed outbox message {MessageId} of type {EventType}",
                            message.Id, message.EventType);
                    }
                    catch (Exception ex)
                    {
                        // Increment retry count
                        message.RetryCount++;
                        await context.SaveChangesAsync(ct);

                        _logger.LogError(ex,
                            "Failed to process outbox message {MessageId}, retry {RetryCount}",
                            message.Id, message.RetryCount);
                    }
                }

                // Cleanup old processed messages (>7 days)
                var cutoff = DateTime.UtcNow.AddDays(-7);
                await context.OutboxMessages
                    .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                    .ExecuteDeleteAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processor error");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }
}
```

**Benefits:**
- ✅ Atomicity: DB write + event publishing trong cùng transaction
- ✅ At-least-once delivery: Events sẽ được publish eventually
- ✅ Retry logic: Failed events được retry tự động
- ✅ Audit trail: Outbox table là audit log

**Trade-offs:**
- ❌ Eventual consistency: Events không publish ngay lập tức
- ❌ Duplicate events: Có thể publish 2 lần (need idempotent handlers)
- ❌ Storage overhead: Outbox table tăng size

**When to use:**
- ✅ Event Sourcing với external event bus
- ✅ CQRS với separate read models
- ✅ Microservices communication
- ❌ Simple monolith không cần events

---

### Decision Matrix: When to Use What

| Pattern | Use When | Don't Use When |
|---------|----------|----------------|
| **Clean Architecture** | Multi-tenant scale (Phase 8), 3+ devs, 2+ years maintenance | MVP (Phase 1-7), 1 dev, <6 months |
| **DDD** | Complex business rules, multiple bounded contexts | Simple CRUD, data-centric apps |
| **CQRS** | Read/write patterns khác nhau, need independent scaling | Simple apps, read/write tương tự |
| **Event Sourcing** | Audit trail critical, temporal queries needed, complex workflows | Simple state, no audit requirements |

**Recommended for Phase 8:**
- ✅ Clean Architecture + DDD: Core foundation
- ✅ CQRS: Cho Product Catalog (search-heavy) và Order Management
- ⚠️ Event Sourcing: Chỉ cho Order workflow (audit trail quan trọng)

**NOT recommended:**
- ❌ Event Sourcing cho Product Catalog (overkill)
- ❌ CQRS cho Tenant Management (simple CRUD)

---

### Implementation Roadmap

**Phase 8.1: Multi-Branch (2 months)**
- Introduce Clean Architecture layers
- No CQRS/Event Sourcing yet

**Phase 8.2: Multi-Tenant (3 months)**
- Complete DDD bounded contexts
- Introduce CQRS cho Product Catalog

**Phase 8.3: Multi-Category (3 months)**
- CQRS cho all read-heavy contexts
- Event Sourcing cho Order workflow

**Phase 8.4: Scaling (2 months)**
- Optimize CQRS read models
- Event store performance tuning
