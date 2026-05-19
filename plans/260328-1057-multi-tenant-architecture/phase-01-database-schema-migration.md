# Phase 1: Database Schema Migration

**Duration:** 3 days
**Cost:** 12M VND
**Status:** Not Started

---

## Overview

Add multi-tenant support to database schema by adding `tenant_id` to all entities and creating tenant management tables.

---

## Requirements

### Functional
- All entities must have `tenant_id` column
- Create `Tenants` table for tenant management
- Create `Branches` table for Facebook Page mapping
- Migrate existing Múi Xù data to first tenant
- Maintain backward compatibility during migration

### Non-Functional
- Zero downtime migration
- Rollback plan if migration fails
- Data integrity preserved

---

## Architecture

### New Tables

**Tenants:**
```csharp
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; }              // "Múi Xù Cosmetics"
    public string Slug { get; set; }              // "mui-xu-cosmetics"
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }

    // Configuration
    public string GeminiApiKey { get; set; }      // Encrypted
    public string SystemPromptPath { get; set; }  // "prompts/mui-xu/system.txt"

    // Navigation
    public ICollection<Branch> Branches { get; set; }
}
```

**Branches:**
```csharp
public class Branch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }              // "Fanpage chính"
    public string FacebookPageId { get; set; }    // From webhook
    public string PageAccessToken { get; set; }   // Encrypted
    public string WebhookVerifyToken { get; set; }// Encrypted
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; }
}
```

### Modified Entities

Add `TenantId` to:
- `Product`
- `ProductVariant`
- `CosmeticsMetadata`
- `SkinProfile`
- `IngredientCompatibility`
- `ConversationSession`
- `ConversationMessage`

**Base class:**
```csharp
public abstract class TenantEntity
{
    public Guid TenantId { get; set; }

    // Navigation (optional, for explicit queries)
    public Tenant Tenant { get; set; }
}
```

---

## Implementation Steps

### Step 1: Create Base Entity (30 min)
```csharp
// src/MessengerWebhook/Data/Entities/TenantEntity.cs
public abstract class TenantEntity
{
    public Guid TenantId { get; set; }
}
```

### Step 2: Create Tenant & Branch Entities (1 hour)
```csharp
// src/MessengerWebhook/Data/Entities/Tenant.cs
// src/MessengerWebhook/Data/Entities/Branch.cs
```

### Step 3: Update Existing Entities (2 hours)
Inherit from `TenantEntity`:
```csharp
public class Product : TenantEntity
{
    // Remove: public Guid TenantId { get; set; } (inherited)
    // Keep existing properties
}
```

Apply to all entities listed above.

### Step 4: Update DbContext (1 hour)
```csharp
// src/MessengerWebhook/Data/MessengerBotDbContext.cs
public DbSet<Tenant> Tenants { get; set; }
public DbSet<Branch> Branches { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Tenant
    modelBuilder.Entity<Tenant>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Slug).IsUnique();
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
    });

    // Branch
    modelBuilder.Entity<Branch>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.FacebookPageId).IsUnique();
        entity.HasIndex(e => new { e.TenantId, e.FacebookPageId });

        entity.HasOne(e => e.Tenant)
            .WithMany(t => t.Branches)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    });

    // Add tenant_id indexes to existing entities
    modelBuilder.Entity<Product>()
        .HasIndex(p => new { p.TenantId, p.Category });

    modelBuilder.Entity<ConversationSession>()
        .HasIndex(s => new { s.TenantId, s.PageScopedUserId });
}
```

### Step 5: Create Migration (30 min)
```bash
dotnet ef migrations add AddMultiTenantSupport \
    --project src/MessengerWebhook \
    --startup-project src/MessengerWebhook \
    --context MessengerBotDbContext
```

**Review migration:**
- Check all tables have `tenant_id` column
- Check indexes created
- Check foreign keys

### Step 6: Create Data Migration Script (2 hours)
```csharp
// src/MessengerWebhook/Data/Migrations/Scripts/MigrateMuiXuToTenant.cs
public class MigrateMuiXuToTenant
{
    public static async Task ExecuteAsync(MessengerBotDbContext context)
    {
        // 1. Create Múi Xù tenant
        var muiXuTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Múi Xù Cosmetics",
            Slug = "mui-xu-cosmetics",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            SystemPromptPath = "prompts/mui-xu/system.txt"
        };
        context.Tenants.Add(muiXuTenant);
        await context.SaveChangesAsync();

        // 2. Create branch (get from appsettings)
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            TenantId = muiXuTenant.Id,
            Name = "Fanpage chính",
            FacebookPageId = Environment.GetEnvironmentVariable("FACEBOOK_PAGE_ID"),
            PageAccessToken = Environment.GetEnvironmentVariable("PAGE_ACCESS_TOKEN"),
            WebhookVerifyToken = Environment.GetEnvironmentVariable("WEBHOOK_VERIFY_TOKEN"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        // 3. Update all existing records with tenant_id
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE products SET tenant_id = {0}", muiXuTenant.Id);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE product_variants SET tenant_id = {0}", muiXuTenant.Id);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE cosmetics_metadata SET tenant_id = {0}", muiXuTenant.Id);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE conversation_sessions SET tenant_id = {0}", muiXuTenant.Id);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE conversation_messages SET tenant_id = {0}", muiXuTenant.Id);

        Console.WriteLine($"✅ Migrated all data to tenant: {muiXuTenant.Name}");
    }
}
```

### Step 7: Apply Migration (1 hour)
```bash
# Backup database first
pg_dump -h localhost -U postgres -d messenger_bot > backup_pre_multitenant.sql

# Apply migration
dotnet ef database update \
    --project src/MessengerWebhook \
    --startup-project src/MessengerWebhook \
    --context MessengerBotDbContext

# Run data migration
dotnet run --project src/MessengerWebhook -- migrate-mui-xu-tenant
```

### Step 8: Verify Migration (1 hour)
```sql
-- Check tenant created
SELECT * FROM tenants;

-- Check branch created
SELECT * FROM branches;

-- Check all products have tenant_id
SELECT COUNT(*) FROM products WHERE tenant_id IS NULL;
-- Should return 0

-- Check indexes
SELECT * FROM pg_indexes WHERE tablename = 'products';
```

---

## Rollback Plan

If migration fails:
```bash
# Restore from backup
psql -h localhost -U postgres -d messenger_bot < backup_pre_multitenant.sql

# Revert migration
dotnet ef database update <previous-migration-name> \
    --project src/MessengerWebhook \
    --context MessengerBotDbContext
```

---

## Testing

### Unit Tests
```csharp
// tests/MessengerWebhook.Tests/Data/TenantEntityTests.cs
[Fact]
public void Product_ShouldInheritFromTenantEntity()
{
    var product = new Product();
    Assert.IsAssignableFrom<TenantEntity>(product);
}
```

### Integration Tests
```csharp
// tests/MessengerWebhook.IntegrationTests/Data/MultiTenantMigrationTests.cs
[Fact]
public async Task Migration_ShouldCreateTenantsTable()
{
    var tableExists = await _context.Database
        .ExecuteSqlRawAsync("SELECT 1 FROM tenants LIMIT 1");
    Assert.True(tableExists >= 0);
}

[Fact]
public async Task Migration_ShouldAddTenantIdToProducts()
{
    var columnExists = await _context.Database.ExecuteSqlRawAsync(@"
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'products' AND column_name = 'tenant_id'
    ");
    Assert.True(columnExists >= 0);
}
```

---

## Success Criteria

- ✅ All entities have `tenant_id` column
- ✅ Tenants and Branches tables created
- ✅ Múi Xù data migrated to first tenant
- ✅ All foreign keys and indexes created
- ✅ No NULL `tenant_id` in any table
- ✅ Backup created before migration
- ✅ Rollback plan tested

---

## Security Considerations

- Encrypt `GeminiApiKey`, `PageAccessToken`, `WebhookVerifyToken` (Phase 4)
- Add NOT NULL constraint to `tenant_id` after migration
- Add CHECK constraint: `tenant_id != '00000000-0000-0000-0000-000000000000'`

---

## Next Steps

After Phase 1 completion:
- Phase 2: Implement request routing with `TenantContext`
- Phase 2: Add EF Core global query filters
