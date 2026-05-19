# Phase 4: Security Hardening

**Duration:** 3 days
**Cost:** 12M VND
**Status:** Not Started
**Depends on:** Phase 1, 2, 3

---

## Overview

Implement security measures to prevent data leakage, encrypt sensitive data, and enforce tenant isolation at database level.

---

## Requirements

### Functional
- PostgreSQL Row-Level Security (RLS)
- Encrypt sensitive fields at rest
- Audit logging for tenant operations
- Automated tenant isolation tests

### Non-Functional
- Zero data leakage between tenants
- Minimal performance overhead (< 5ms)
- Compliance-ready (GDPR, data protection)

---

## Implementation Steps

### Step 1: PostgreSQL Row-Level Security (1 day)

#### 1.1 Enable RLS on Tables (2 hours)
```sql
-- Enable RLS on all tenant tables
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_variants ENABLE ROW LEVEL SECURITY;
ALTER TABLE cosmetics_metadata ENABLE ROW LEVEL SECURITY;
ALTER TABLE conversation_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE conversation_messages ENABLE ROW LEVEL SECURITY;
ALTER TABLE skin_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE ingredient_compatibilities ENABLE ROW LEVEL SECURITY;
```

#### 1.2 Create RLS Policies (2 hours)
```sql
-- Policy: Only allow access to current tenant's data
CREATE POLICY tenant_isolation_policy ON products
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_policy ON product_variants
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_policy ON conversation_sessions
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- Repeat for all tenant tables
```

#### 1.3 Set Tenant Context in DbContext (2 hours)
```csharp
// src/MessengerWebhook/Data/MessengerBotDbContext.cs
public class MessengerBotDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Set PostgreSQL session variable before any query
        if (_tenantContext.IsResolved)
        {
            await Database.ExecuteSqlRawAsync(
                "SET LOCAL app.current_tenant_id = {0}",
                _tenantContext.TenantId.ToString());
        }

        return await base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        if (_tenantContext.IsResolved)
        {
            Database.ExecuteSqlRaw(
                "SET LOCAL app.current_tenant_id = {0}",
                _tenantContext.TenantId.ToString());
        }

        return base.SaveChanges();
    }
}
```

#### 1.4 Test RLS (2 hours)
```sql
-- Test as tenant 1
SET app.current_tenant_id = 'tenant-1-uuid';
SELECT * FROM products; -- Should only see tenant 1 products

-- Test as tenant 2
SET app.current_tenant_id = 'tenant-2-uuid';
SELECT * FROM products; -- Should only see tenant 2 products

-- Test without tenant context
RESET app.current_tenant_id;
SELECT * FROM products; -- Should see nothing (RLS blocks)
```

---

### Step 2: Encrypt Sensitive Fields (1 day)

#### 2.1 Add Data Protection (1 hour)
```bash
dotnet add package Microsoft.AspNetCore.DataProtection
dotnet add package Microsoft.AspNetCore.DataProtection.StackExchangeRedis
```

#### 2.2 Configure Data Protection (1 hour)
```csharp
// src/MessengerWebhook/Program.cs
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
    .SetApplicationName("MessengerBot");
```

#### 2.3 Create Encryption Value Converter (2 hours)
```csharp
// src/MessengerWebhook/Data/Converters/EncryptedStringConverter.cs
public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IDataProtector protector)
        : base(
            v => protector.Protect(v),
            v => protector.Unprotect(v))
    { }
}

// src/MessengerWebhook/Data/Converters/EncryptedStringConverterFactory.cs
public class EncryptedStringConverterFactory
{
    private readonly IDataProtectionProvider _provider;

    public EncryptedStringConverterFactory(IDataProtectionProvider provider)
    {
        _provider = provider;
    }

    public EncryptedStringConverter Create(string purpose)
    {
        var protector = _provider.CreateProtector(purpose);
        return new EncryptedStringConverter(protector);
    }
}
```

#### 2.4 Apply Encryption to Entities (2 hours)
```csharp
// src/MessengerWebhook/Data/MessengerBotDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var encryptionFactory = new EncryptedStringConverterFactory(_dataProtectionProvider);

    // Encrypt sensitive fields
    modelBuilder.Entity<Tenant>()
        .Property(t => t.GeminiApiKey)
        .HasConversion(encryptionFactory.Create("Tenant.GeminiApiKey"));

    modelBuilder.Entity<Branch>()
        .Property(b => b.PageAccessToken)
        .HasConversion(encryptionFactory.Create("Branch.PageAccessToken"));

    modelBuilder.Entity<Branch>()
        .Property(b => b.WebhookVerifyToken)
        .HasConversion(encryptionFactory.Create("Branch.WebhookVerifyToken"));
}
```

#### 2.5 Migration for Encrypted Fields (1 hour)
```bash
# Create migration
dotnet ef migrations add EncryptSensitiveFields

# Manual step: Encrypt existing data
# Run script to re-save all entities (triggers encryption)
```

---

### Step 3: Audit Logging (1 day)

#### 3.1 Create Audit Log Entity (1 hour)
```csharp
// src/MessengerWebhook/Data/Entities/AuditLog.cs
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EntityType { get; set; }      // "Product", "Session"
    public string EntityId { get; set; }
    public string Action { get; set; }          // "Create", "Update", "Delete"
    public string? UserId { get; set; }         // Optional: who made the change
    public string? Changes { get; set; }        // JSON: before/after
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
}
```

#### 3.2 Create Audit Interceptor (2 hours)
```csharp
// src/MessengerWebhook/Data/Interceptors/AuditInterceptor.cs
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context == null) return result;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                       e.State == EntityState.Modified ||
                       e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetEntityId(entry),
                Action = entry.State.ToString(),
                Changes = SerializeChanges(entry),
                Timestamp = DateTime.UtcNow,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
            };

            context.Set<AuditLog>().Add(auditLog);
        }

        return result;
    }

    private string GetEntityId(EntityEntry entry)
    {
        var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return keyProperty?.CurrentValue?.ToString() ?? "unknown";
    }

    private string SerializeChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object>();

        foreach (var property in entry.Properties)
        {
            if (property.IsModified)
            {
                changes[property.Metadata.Name] = new
                {
                    Before = property.OriginalValue,
                    After = property.CurrentValue
                };
            }
        }

        return JsonSerializer.Serialize(changes);
    }
}
```

#### 3.3 Register Interceptor (30 min)
```csharp
// src/MessengerWebhook/Program.cs
builder.Services.AddDbContext<MessengerBotDbContext>(options =>
{
    options.UseNpgsql(connectionString)
        .AddInterceptors(new AuditInterceptor(tenantContext, httpContextAccessor));
});
```

#### 3.4 Audit Log Queries (1 hour)
```csharp
// src/MessengerWebhook/Services/Audit/IAuditLogService.cs
public interface IAuditLogService
{
    Task<List<AuditLog>> GetLogsAsync(Guid tenantId, DateTime from, DateTime to);
    Task<List<AuditLog>> GetEntityLogsAsync(string entityType, string entityId);
}
```

---

### Step 4: Automated Tenant Isolation Tests (1 day)

#### 4.1 Create Test Base Class (1 hour)
```csharp
// tests/MessengerWebhook.IntegrationTests/TenantIsolationTestBase.cs
public abstract class TenantIsolationTestBase : IAsyncLifetime
{
    protected Guid Tenant1Id;
    protected Guid Tenant2Id;
    protected MessengerBotDbContext Context;

    public async Task InitializeAsync()
    {
        Tenant1Id = await CreateTenantAsync("Tenant 1");
        Tenant2Id = await CreateTenantAsync("Tenant 2");
    }

    protected void SetTenantContext(Guid tenantId)
    {
        // Mock ITenantContext
    }
}
```

#### 4.2 Create Isolation Tests (3 hours)
```csharp
// tests/MessengerWebhook.IntegrationTests/Security/TenantIsolationTests.cs
public class TenantIsolationTests : TenantIsolationTestBase
{
    [Fact]
    public async Task Products_ShouldNotLeakBetweenTenants()
    {
        // Arrange
        var product1 = await CreateProductAsync(Tenant1Id, "Product 1");
        var product2 = await CreateProductAsync(Tenant2Id, "Product 2");

        // Act: Query as Tenant 1
        SetTenantContext(Tenant1Id);
        var results = await Context.Products.ToListAsync();

        // Assert
        Assert.Contains(product1, results);
        Assert.DoesNotContain(product2, results);
    }

    [Fact]
    public async Task Sessions_ShouldNotLeakBetweenTenants()
    {
        var session1 = await CreateSessionAsync(Tenant1Id);
        var session2 = await CreateSessionAsync(Tenant2Id);

        SetTenantContext(Tenant1Id);
        var results = await Context.ConversationSessions.ToListAsync();

        Assert.Contains(session1, results);
        Assert.DoesNotContain(session2, results);
    }

    [Fact]
    public async Task RLS_ShouldBlockQueriesWithoutTenantContext()
    {
        await CreateProductAsync(Tenant1Id, "Product");

        // Don't set tenant context
        var results = await Context.Products.ToListAsync();

        Assert.Empty(results); // RLS blocks all results
    }
}
```

#### 4.3 Run Tests in CI/CD (1 hour)
```yaml
# .github/workflows/security-tests.yml
name: Security Tests

on: [push, pull_request]

jobs:
  tenant-isolation:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run tenant isolation tests
        run: |
          dotnet test --filter "Category=TenantIsolation"
```

---

## Success Criteria

- ✅ PostgreSQL RLS enabled on all tenant tables
- ✅ RLS policies block cross-tenant queries
- ✅ Sensitive fields encrypted at rest
- ✅ Audit logs capture all tenant operations
- ✅ 100% tenant isolation tests passing
- ✅ Performance overhead < 5ms

---

## Security Checklist

- [ ] RLS enabled on all tenant tables
- [ ] RLS policies tested manually
- [ ] Sensitive fields encrypted (API keys, tokens)
- [ ] Audit logging captures all changes
- [ ] Automated tests verify isolation
- [ ] Security review completed
- [ ] Penetration testing (optional)

---

## Next Steps

After Phase 4 completion:
- Phase 5: Testing & validation
- Security audit report
