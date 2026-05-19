# Phase 1 Fix: Cosmetics Schema Implementation

**Date**: 2026-03-21
**Duration**: ~45 minutes
**Status**: ✅ Completed

---

## Summary

Fixed Phase 1 implementation gaps by updating database schema from clothing to cosmetics domain. Added missing entities, repositories, and generated migration.

---

## Changes Made

### 1. Product Entity (Product.cs)
**Added cosmetics-specific fields:**
- `Brand` (string)
- `Category` (enum: Cosmetics/Fashion/Electronics)
- `IngredientsJson` (jsonb) - List of ingredients
- `SkinTypesJson` (jsonb) - oily, dry, combination, sensitive
- `SkinConcernsJson` (jsonb) - acne, aging, hyperpigmentation
- `pH` (double?) - pH level
- `Texture` (string?) - cream, gel, serum, oil
- `ContraindicationsJson` (jsonb) - contraindication warnings

### 2. ProductVariant Entity (ProductVariant.cs)
**Replaced clothing fields with cosmetics:**
- ❌ Removed: `ColorId`, `SizeId` (clothing-specific)
- ✅ Added: `VolumeML` (int) - 30, 50, 100, 200ml
- ✅ Added: `Texture` (string) - cream, gel, serum, oil

### 3. New Entities Created

**SkinProfile.cs** - User skin analysis
```csharp
- SessionId (FK to ConversationSession)
- SkinType (string) - oily, dry, combination, sensitive
- ConcernsJson (jsonb) - List of skin concerns
- SensitivitiesJson (jsonb) - List of ingredient sensitivities
- ExtractedAt, UpdatedAt (DateTime)
```

**IngredientCompatibility.cs** - Contraindication rules
```csharp
- Ingredient1, Ingredient2 (string)
- Type (enum) - Contraindicated, Caution, Synergistic
- Reason (string)
- CreatedAt (DateTime)
```

**ConversationMessage.cs** - 30-day history retention
```csharp
- SessionId (FK to ConversationSession)
- Role (string) - "user" or "model"
- Content (string)
- CreatedAt (DateTime)
```

### 4. Repositories Created

**ISkinProfileRepository + SkinProfileRepository**
- `GetBySessionIdAsync()` - Get skin profile for session
- `CreateAsync()` - Create new skin profile
- `UpdateAsync()` - Update existing profile
- `DeleteAsync()` - Delete profile

**IConversationMessageRepository + ConversationMessageRepository**
- `GetBySessionIdAsync(limit=10)` - Get last N messages
- `CreateAsync()` - Save message
- `DeleteOlderThanAsync()` - Cleanup old messages (30-day retention)

**IIngredientCompatibilityRepository + IngredientCompatibilityRepository**
- `GetCompatibilitiesAsync(ingredient)` - Get all rules for ingredient
- `GetByIngredientsAsync(ing1, ing2)` - Check specific pair
- `CreateAsync()` - Add new rule
- `GetAllAsync()` - Get all rules

### 5. DbContext Updates (MessengerBotDbContext.cs)

**Added DbSets:**
- `DbSet<ConversationMessage> ConversationMessages`
- `DbSet<SkinProfile> SkinProfiles`
- `DbSet<IngredientCompatibility> IngredientCompatibilities`

**Added Indexes:**
- ConversationMessage: SessionId, CreatedAt
- SkinProfile: SessionId (unique)
- IngredientCompatibility: (Ingredient1, Ingredient2)
- ProductVariant: (ProductId, VolumeML, Texture) unique

**Added Relationships:**
- ConversationMessage → ConversationSession (cascade delete)
- SkinProfile → ConversationSession (cascade delete)

### 6. Repository Registration (Program.cs)

```csharp
builder.Services.AddScoped<ISkinProfileRepository, SkinProfileRepository>();
builder.Services.AddScoped<IConversationMessageRepository, ConversationMessageRepository>();
builder.Services.AddScoped<IIngredientCompatibilityRepository, IngredientCompatibilityRepository>();
```

### 7. Fixed ProductRepository

Updated `IProductRepository` and `ProductRepository`:
- Changed `GetByCategoryAsync(string)` → `GetByCategoryAsync(ProductCategory)`
- Removed Color/Size includes (no longer exist in ProductVariant)

### 8. Migration Generated

**File**: `20260321120339_UpdateSchemaForCosmetics.cs`

**Changes:**
- Drop foreign keys: ProductVariant → Color, Size
- Add columns: Product (Brand, IngredientsJson, SkinTypesJson, etc.)
- Add columns: ProductVariant (VolumeML, Texture)
- Create tables: ConversationMessages, SkinProfiles, IngredientCompatibilities
- Update indexes for new schema

**Warning**: Data loss possible if existing clothing data exists (ColorId/SizeId dropped).

---

## Build Status

✅ **Compilation**: Success (0 errors, 0 warnings)
⚠️ **Migration Apply**: Skipped (PostgreSQL not running on localhost:5433)

---

## Next Steps

### To Apply Migration

**Option A: PostgreSQL (Recommended for RAG)**
```bash
# Start PostgreSQL on port 5433
docker run -d -p 5433:5432 -e POSTGRES_PASSWORD=yourpassword postgres:16

# Apply migration
cd src/MessengerWebhook
dotnet ef database update
```

**Option B: SQL Server**
```bash
# Update connection string in appsettings.json
# Change provider in Program.cs: UseNpgsql → UseSqlServer
# Regenerate migration
dotnet ef migrations remove
dotnet ef migrations add UpdateSchemaForCosmetics
dotnet ef database update
```

### Ready for Implementation

Phase 1 is now complete. Can proceed with:
- ✅ **Phase 2.5: RAG Layer** (blocker removed - IngredientsJson field exists)
- ✅ **Phase 3: State Machine** (ConversationMessage entity exists)
- ✅ **Phase 4: Product Catalog** (cosmetics schema ready)
- ✅ **Phase 5: Conversation Flows** (SkinProfile entity exists)

---

## Files Modified

**Entities:**
- `Data/Entities/Product.cs` - Added 7 cosmetics fields
- `Data/Entities/ProductVariant.cs` - Replaced Color/Size with Volume/Texture
- `Data/Entities/SkinProfile.cs` - NEW
- `Data/Entities/IngredientCompatibility.cs` - NEW
- `Data/Entities/ConversationMessage.cs` - NEW

**Repositories:**
- `Data/Repositories/IProductRepository.cs` - Updated signature
- `Data/Repositories/ProductRepository.cs` - Fixed implementation
- `Data/Repositories/ISkinProfileRepository.cs` - NEW
- `Data/Repositories/SkinProfileRepository.cs` - NEW
- `Data/Repositories/IConversationMessageRepository.cs` - NEW
- `Data/Repositories/ConversationMessageRepository.cs` - NEW
- `Data/Repositories/IIngredientCompatibilityRepository.cs` - NEW
- `Data/Repositories/IngredientCompatibilityRepository.cs` - NEW

**Infrastructure:**
- `Data/MessengerBotDbContext.cs` - Added 3 DbSets, indexes, relationships
- `Program.cs` - Registered 3 new repositories
- `Data/Migrations/20260321120339_UpdateSchemaForCosmetics.cs` - NEW

---

## Verification

**Compilation**: ✅ Pass
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.75
```

**Migration Generation**: ✅ Success
```
Done. To undo this action, use 'ef migrations remove'
```

**Schema Completeness**: ✅ 100%
- All 7 cosmetics fields added to Product
- All 2 cosmetics fields added to ProductVariant
- All 3 missing entities created
- All 3 repositories implemented
- DbContext fully configured

---

## Unresolved Questions

1. Database choice: PostgreSQL (for pgvector) or SQL Server (existing infrastructure)?
2. Existing data: Migrate or drop? (ColorId/SizeId → VolumeML/Texture)
3. Keep Color/Size tables for Phase 8 multi-category support?
