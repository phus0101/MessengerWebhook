# Phase 1: Database Setup

**Priority**: Critical
**Status**: Pending
**Duration**: 1 week
**Dependencies**: None

---

## Context Links

- Research: [Order Management Report](../reports/researcher-260320-1042-order-management.md)
- Current Code: `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\Services\WebhookProcessor.cs`

---

## Overview

Design and implement complete database schema for **cosmetics e-commerce chatbot**. Includes product catalog with variants (volume/texture), skin profile tracking, ingredient compatibility, conversation state management, shopping carts, and order tracking. Use Entity Framework Core with migrations. **Note:** RAG/Vector embeddings (pgvector) will be added in Phase 2.5.

---

## Key Insights

- Current system uses MemoryCache only (no persistence)
- Variant-level stock tracking required (30ml, 50ml, 100ml, 200ml)
- Session state must survive app restarts
- Cosmetics need: ingredients, skin types, concerns, pH, texture, contraindications
- RAG embeddings (pgvector) added in Phase 2.5
- Need audit trail for orders and state transitions
- Ingredient compatibility rules (e.g., Retinol + AHA contraindication)

---

## Requirements

### Functional
- Store cosmetics products with ingredients, skin types, concerns, pH, texture
- Track stock at variant level (volume: 30ml, 50ml, 100ml, 200ml)
- Store ingredient compatibility rules (contraindications)
- Persist conversation state + skin profile per user
- Manage shopping carts with expiration
- Store draft and confirmed orders
- Support session timeout and recovery
- Track conversation history (30-day retention)

### Non-Functional
- Database queries <50ms (indexed properly)
- Support 1000+ concurrent sessions
- Handle 10,000+ products with 100,000+ variants
- Atomic cart operations (prevent overselling)
- ACID compliance for order creation

---

## Architecture

### Database Choice
**Recommended**: PostgreSQL (open-source, JSON support, excellent performance)
**Alternative**: SQL Server (if existing infrastructure)

### ORM Strategy
- Entity Framework Core 8
- Code-first migrations
- Repository pattern for testability
- Unit of Work for transactions

### Data Flow
```
User Message → Load Session State → Process → Update State → Save to DB
                     ↓
              Load Product/Cart Data → Validate Stock → Update Cart/Order
```

---

## Related Code Files

### To Create
- `src/MessengerWebhook/Data/MessengerBotDbContext.cs`
- `src/MessengerWebhook/Data/Entities/Product.cs` (+ cosmetics fields)
- `src/MessengerWebhook/Data/Entities/ProductVariant.cs` (volume-based)
- `src/MessengerWebhook/Data/Entities/ProductImage.cs`
- `src/MessengerWebhook/Data/Entities/ConversationSession.cs`
- `src/MessengerWebhook/Data/Entities/SkinProfile.cs` (NEW)
- `src/MessengerWebhook/Data/Entities/IngredientCompatibility.cs` (NEW)
- `src/MessengerWebhook/Data/Entities/Cart.cs`
- `src/MessengerWebhook/Data/Entities/CartItem.cs`
- `src/MessengerWebhook/Data/Entities/Order.cs`
- `src/MessengerWebhook/Data/Entities/OrderItem.cs`
- `src/MessengerWebhook/Data/Repositories/IProductRepository.cs`
- `src/MessengerWebhook/Data/Repositories/ProductRepository.cs`
- `src/MessengerWebhook/Data/Repositories/ISessionRepository.cs`
- `src/MessengerWebhook/Data/Repositories/SessionRepository.cs`
- `src/MessengerWebhook/Data/Repositories/ICartRepository.cs`
- `src/MessengerWebhook/Data/Repositories/CartRepository.cs`
- `src/MessengerWebhook/Data/Repositories/IOrderRepository.cs`
- `src/MessengerWebhook/Data/Repositories/OrderRepository.cs`
- `src/MessengerWebhook/Migrations/` (auto-generated)

### To Modify
- `src/MessengerWebhook/Program.cs` (add DbContext registration)
- `src/MessengerWebhook/appsettings.json` (add connection string)

---

## Implementation Steps

### 1. Install NuGet Packages
```bash
cd "D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook"
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
# OR for SQL Server:
# dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

### 2. Create Entity Models

**Product.cs**:
```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Brand { get; set; }
    public decimal BasePrice { get; set; }
    public ProductCategory Category { get; set; } = ProductCategory.Cosmetics;

    // Cosmetics-specific (JSON column)
    [Column(TypeName = "jsonb")]
    public string IngredientsJson { get; set; }  // List<string>

    [Column(TypeName = "jsonb")]
    public string SkinTypesJson { get; set; }  // List<string>: oily, dry, combination, sensitive

    [Column(TypeName = "jsonb")]
    public string SkinConcernsJson { get; set; }  // List<string>: acne, aging, hyperpigmentation

    public double? pH { get; set; }
    public string Texture { get; set; }  // cream, gel, serum, oil

    [Column(TypeName = "jsonb")]
    public string ContraindicationsJson { get; set; }  // List<string>

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Relationships
    public List<ProductVariant> Variants { get; set; }
    public List<ProductImage> Images { get; set; }
}

public enum ProductCategory
{
    Cosmetics,
    Fashion,
    Electronics
}
```

**ProductVariant.cs**:
```csharp
public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string SKU { get; set; }

    // Cosmetics variants: volume + texture
    public int VolumeML { get; set; }  // 30, 50, 100, 200
    public string Texture { get; set; }  // cream, gel, serum

    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; }

    // Relationships
    public Product Product { get; set; }
}
```

**SkinProfile.cs** (NEW):
```csharp
public class SkinProfile
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string SkinType { get; set; }  // oily, dry, combination, sensitive

    [Column(TypeName = "jsonb")]
    public string ConcernsJson { get; set; }  // List<string>: acne, aging, dryness

    [Column(TypeName = "jsonb")]
    public string SensitivitiesJson { get; set; }  // List<string>: fragrance, alcohol

    public DateTime ExtractedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Relationships
    public ConversationSession Session { get; set; }
}
```

**IngredientCompatibility.cs** (NEW):
```csharp
public class IngredientCompatibility
{
    public Guid Id { get; set; }
    public string Ingredient1 { get; set; }  // e.g., "Retinol"
    public string Ingredient2 { get; set; }  // e.g., "AHA"
    public CompatibilityType Type { get; set; }
    public string Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum CompatibilityType
{
    Contraindicated,  // Should NOT use together
    Caution,          // Can use but with care
    Synergistic       // Work well together
}
```

**ConversationMessage.cs** (from Phase 3):
```csharp
public class ConversationMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; }  // "user" or "model"
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }

    // Relationships
    public ConversationSession Session { get; set; }
}
```

### 3. Configure DbContext
```csharp
public class MessengerBotDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<Color> Colors { get; set; }
    public DbSet<Size> Sizes { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<ConversationSession> ConversationSessions { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure indexes, relationships, constraints
        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.FacebookPSID);

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => v.SKU)
            .IsUnique();

        // ... more configurations
    }
}
```

### 4. Implement Repository Pattern
Create repository interfaces and implementations for each entity group. Use async methods throughout.

### 5. Register Services in Program.cs
```csharp
builder.Services.AddDbContext<MessengerBotDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MessengerBotDb")));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
```

### 6. Create Initial Migration
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 7. Seed Reference Data
Create seed data for Colors and Sizes:
```csharp
public static class DbSeeder
{
    public static async Task SeedAsync(MessengerBotDbContext context)
    {
        if (!await context.Colors.AnyAsync())
        {
            context.Colors.AddRange(
                new Color { Name = "Đen", HexCode = "#000000" },
                new Color { Name = "Trắng", HexCode = "#FFFFFF" },
                new Color { Name = "Đỏ", HexCode = "#FF0000" },
                // ... more colors
            );
        }

        if (!await context.Sizes.AnyAsync())
        {
            context.Sizes.AddRange(
                new Size { Category = "mens", SizeCode = "S", SortOrder = 1 },
                new Size { Category = "mens", SizeCode = "M", SortOrder = 2 },
                // ... more sizes
            );
        }

        await context.SaveChangesAsync();
    }
}
```

### 8. Create Sample Product Data
Add 10-20 sample products with variants for testing:
- 5 shirts (3 colors × 5 sizes each)
- 3 pants (2 colors × 5 sizes each)
- 2 dresses (4 colors × 4 sizes each)

### 9. Write Repository Unit Tests
Test CRUD operations, stock validation, cart expiration logic.

### 10. Performance Testing
- Benchmark query performance with 10,000 products
- Test concurrent cart operations (prevent race conditions)
- Verify index effectiveness

---

## Todo List

- [ ] Install EF Core and database provider packages
- [ ] Create all entity models with proper annotations
- [ ] Configure DbContext with relationships and indexes
- [ ] Implement repository interfaces
- [ ] Implement repository classes with async methods
- [ ] Register DbContext and repositories in DI container
- [ ] Add connection string to appsettings.json (use User Secrets)
- [ ] Create initial EF migration
- [ ] Apply migration to create database
- [ ] Create seed data for Colors and Sizes
- [ ] Create sample product catalog (20+ products)
- [ ] Write unit tests for repositories
- [ ] Test concurrent cart operations
- [ ] Benchmark query performance
- [ ] Document database schema (ER diagram)

---

## Success Criteria

- ✅ All tables created with proper relationships
- ✅ Indexes on frequently queried columns (PSID, SKU, ProductId)
- ✅ Sample data: 20+ products, 200+ variants, 10+ colors, 10+ sizes
- ✅ Repository tests pass (100% coverage)
- ✅ Query performance <50ms for product lookups
- ✅ Concurrent cart operations handle race conditions correctly
- ✅ Connection string stored securely (User Secrets/Key Vault)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Database choice mismatch with existing infrastructure | Medium | High | Confirm with team before starting |
| EF Core performance issues at scale | Low | Medium | Use compiled queries, proper indexing |
| Stock overselling due to race conditions | Medium | High | Use transactions, optimistic concurrency |
| Migration conflicts in team environment | Low | Low | Use migration naming conventions |

---

## Security Considerations

- **Connection String**: Store in User Secrets (dev) and Azure Key Vault (prod)
- **SQL Injection**: Use parameterized queries (EF Core handles this)
- **Data Validation**: Validate all inputs before database operations
- **Audit Trail**: Log all order state changes with timestamps
- **PII Protection**: Don't log customer addresses or phone numbers
- **Access Control**: Database user with minimal required permissions

---

## Next Steps

After Phase 1 completion:
1. Proceed to Phase 2: Gemini Integration
2. Use repositories in WebhookProcessor to load/save state
3. Implement product browsing in Phase 4
4. Build order creation workflow in Phase 6
