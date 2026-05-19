# Phase 4: Product Catalog

**Priority**: High
**Status**: Pending
**Duration**: 2 weeks
**Dependencies**: Phase 1 (Database Setup), Phase 2.5 (RAG Layer)

---

## Context Links

- Research: [Order Management Report](../reports/researcher-260320-1042-order-management.md)
- Database: [Phase 1 - Database Setup](./phase-01-database-setup.md)
- Current Code: `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\Services\MessengerService.cs`

---

## Overview

Implement semantic product search using RAG (Phase 2.5) for ingredient-based matching. Build cosmetics-specific Messenger templates for product display with skin-type compatibility. Handle variant selection (volume/texture) and stock validation.

---

## Key Insights

- Messenger supports Generic Template (carousel), List Template, Button Template
- Product images critical for conversion (use ProductImages table)
- Variant-level stock tracking prevents overselling (volume: 30ml/50ml/100ml/200ml)
- Semantic search required: "da dầu mụn" → products with BHA, niacinamide
- RAG embeddings enable ingredient-based matching
- Skin-type compatibility filtering essential
- Support Vietnamese product names and ingredient names

---

## Requirements

### Functional
- Semantic search by skin concerns: "da khô nhạy cảm" → hyaluronic acid products
- Search by ingredients: "niacinamide" → all products containing it
- Filter by skin type compatibility (oily, dry, combination, sensitive)
- Display product details with ingredients, pH, texture
- Show available variants (volume: 30ml/50ml/100ml/200ml, texture: cream/gel/serum)
- Validate stock before selection
- Check ingredient compatibility (warn if contraindications)
- Support pagination for large catalogs

### Non-Functional
- Semantic search query <200ms (p95)
- Support 10,000+ products with embeddings
- Image loading <2s
- Handle concurrent stock checks
- Cache search results (5 min TTL)
- RAG accuracy >85% for ingredient matching

---

## Architecture

### Service Layer
```
Services/
├── Products/
│   ├── IProductSearchService.cs
│   ├── ProductSearchService.cs (uses RAG)
│   ├── IIngredientService.cs
│   ├── IngredientService.cs (compatibility checking)
│   └── Models/
│       ├── ProductDto.cs
│       ├── VariantDto.cs
│       ├── ProductSearchRequest.cs
│       └── IngredientCompatibilityResult.cs
├── Messenger/
│   ├── ITemplateBuilder.cs
│   ├── TemplateBuilder.cs
│   └── Templates/
│       ├── CosmeticsGenericTemplate.cs
│       ├── IngredientListTemplate.cs
│       └── VariantButtonTemplate.cs
```

### Data Flow
```
User: "Kem dưỡng cho da dầu mụn"
  ↓
1. Extract intent: skin type (oily), concern (acne)
  ↓
2. Generate query embedding (text-embedding-004)
  ↓
3. Vector similarity search (pgvector)
  ↓
4. Filter by skin type + ingredients (BHA, niacinamide)
  ↓
5. Build Generic Template with top 5 products
  ↓
6. User selects → Show variants (volumes) → Add to cart
```

---

## Related Code Files

### To Create
- `src/MessengerWebhook/Services/Products/IProductSearchService.cs`
- `src/MessengerWebhook/Services/Products/ProductSearchService.cs` (RAG integration)
- `src/MessengerWebhook/Services/Products/IIngredientService.cs`
- `src/MessengerWebhook/Services/Products/IngredientService.cs`
- `src/MessengerWebhook/Services/Products/Models/ProductDto.cs`
- `src/MessengerWebhook/Services/Products/Models/VariantDto.cs`
- `src/MessengerWebhook/Services/Products/Models/ProductSearchRequest.cs`
- `src/MessengerWebhook/Services/Products/Models/IngredientCompatibilityResult.cs`
- `src/MessengerWebhook/Services/Messenger/ITemplateBuilder.cs`
- `src/MessengerWebhook/Services/Messenger/TemplateBuilder.cs`
- `src/MessengerWebhook/Services/Messenger/Templates/CosmeticsGenericTemplate.cs`
- `src/MessengerWebhook/Services/Messenger/Templates/IngredientListTemplate.cs`
- `src/MessengerWebhook/Services/Messenger/Templates/VariantButtonTemplate.cs`
- `src/MessengerWebhook/Services/Messenger/Templates/QuickReply.cs`

### To Modify
- `src/MessengerWebhook/Services/IMessengerService.cs` (add template methods)
- `src/MessengerWebhook/Services/MessengerService.cs` (implement template sending)
- `src/MessengerWebhook/Program.cs` (register product services)

---

## Implementation Steps

### 1. Create Product DTOs
```csharp
public class ProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int AvailableVariantsCount { get; set; }
}

public class VariantDto
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public string SizeCode { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}
```

### 2. Implement IProductService
```csharp
public interface IProductService
{
    Task<List<ProductDto>> GetByCategoryAsync(string category, int page = 1, int pageSize = 10);
    Task<List<ProductDto>> SearchAsync(string query, int page = 1, int pageSize = 10);
    Task<ProductDto?> GetByIdAsync(int productId);
    Task<List<string>> GetCategoriesAsync();
    Task<List<ProductDto>> GetFeaturedAsync(int count = 5);
}
```

### 3. Implement ProductService
```csharp
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductService> _logger;

    public async Task<List<ProductDto>> GetByCategoryAsync(string category, int page = 1, int pageSize = 10)
    {
        var cacheKey = $"products:category:{category}:page:{page}";
        if (_cache.TryGetValue(cacheKey, out List<ProductDto>? cached))
        {
            return cached!;
        }

        var products = await _productRepo.GetByCategoryAsync(category, page, pageSize);
        var dtos = products.Select(p => new ProductDto
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Description = p.Description,
            Category = p.Category,
            BasePrice = p.BasePrice,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            AvailableVariantsCount = p.Variants.Count(v => v.IsAvailable && v.StockQuantity > 0)
        }).ToList();

        _cache.Set(cacheKey, dtos, TimeSpan.FromMinutes(10));
        return dtos;
    }

    public async Task<List<ProductDto>> SearchAsync(string query, int page = 1, int pageSize = 10)
    {
        var products = await _productRepo.SearchAsync(query, page, pageSize);
        return products.Select(p => new ProductDto
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Description = p.Description,
            Category = p.Category,
            BasePrice = p.BasePrice,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            AvailableVariantsCount = p.Variants.Count(v => v.IsAvailable && v.StockQuantity > 0)
        }).ToList();
    }
}
```

### 4. Implement IVariantService
```csharp
public interface IVariantService
{
    Task<List<VariantDto>> GetByProductIdAsync(int productId);
    Task<List<string>> GetAvailableColorsAsync(int productId);
    Task<List<string>> GetAvailableSizesAsync(int productId, string colorName);
    Task<VariantDto?> GetByIdAsync(int variantId);
    Task<bool> IsAvailableAsync(int variantId, int quantity = 1);
    Task<bool> ReserveStockAsync(int variantId, int quantity);
    Task<bool> ReleaseStockAsync(int variantId, int quantity);
}
```

### 5. Implement VariantService
```csharp
public class VariantService : IVariantService
{
    private readonly IProductRepository _productRepo;
    private readonly ILogger<VariantService> _logger;

    public async Task<List<string>> GetAvailableColorsAsync(int productId)
    {
        var variants = await _productRepo.GetVariantsByProductIdAsync(productId);
        return variants
            .Where(v => v.IsAvailable && v.StockQuantity > 0)
            .Select(v => v.Color.Name)
            .Distinct()
            .ToList();
    }

    public async Task<List<string>> GetAvailableSizesAsync(int productId, string colorName)
    {
        var variants = await _productRepo.GetVariantsByProductIdAsync(productId);
        return variants
            .Where(v => v.IsAvailable && v.StockQuantity > 0 && v.Color.Name == colorName)
            .Select(v => v.Size.SizeCode)
            .OrderBy(s => s)
            .ToList();
    }

    public async Task<bool> IsAvailableAsync(int variantId, int quantity = 1)
    {
        var variant = await _productRepo.GetVariantByIdAsync(variantId);
        return variant != null && variant.IsAvailable && variant.StockQuantity >= quantity;
    }

    public async Task<bool> ReserveStockAsync(int variantId, int quantity)
    {
        // Use transaction to prevent race conditions
        using var transaction = await _productRepo.BeginTransactionAsync();
        try
        {
            var variant = await _productRepo.GetVariantByIdAsync(variantId);
            if (variant == null || variant.StockQuantity < quantity)
            {
                return false;
            }

            variant.StockQuantity -= quantity;
            await _productRepo.UpdateVariantAsync(variant);
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### 6. Create Messenger Template Models
```csharp
public class GenericTemplate
{
    public string TemplateType => "generic";
    public List<GenericElement> Elements { get; set; } = new();
}

public class GenericElement
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public List<TemplateButton> Buttons { get; set; } = new();
}

public class TemplateButton
{
    public string Type { get; set; } = "postback"; // or "web_url"
    public string Title { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string? Url { get; set; }
}

public class QuickReply
{
    public string ContentType => "text";
    public string Title { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
```

### 7. Implement ITemplateBuilder
```csharp
public interface ITemplateBuilder
{
    object BuildProductCarousel(List<ProductDto> products);
    object BuildColorSelection(List<string> colors, int productId);
    object BuildSizeSelection(List<string> sizes, int productId, string color);
    object BuildCartSummary(List<CartItemDto> items, decimal total);
}
```

### 8. Implement TemplateBuilder
```csharp
public class TemplateBuilder : ITemplateBuilder
{
    public object BuildProductCarousel(List<ProductDto> products)
    {
        var elements = products.Select(p => new GenericElement
        {
            Title = p.Name,
            Subtitle = $"{p.BasePrice:N0} VNĐ\n{p.Description}",
            ImageUrl = p.ImageUrl,
            Buttons = new List<TemplateButton>
            {
                new()
                {
                    Type = "postback",
                    Title = "Xem chi tiết",
                    Payload = $"VIEW_PRODUCT_{p.ProductId}"
                },
                new()
                {
                    Type = "postback",
                    Title = "Thêm vào giỏ",
                    Payload = $"ADD_TO_CART_{p.ProductId}"
                }
            }
        }).ToList();

        return new
        {
            attachment = new
            {
                type = "template",
                payload = new
                {
                    template_type = "generic",
                    elements
                }
            }
        };
    }

    public object BuildColorSelection(List<string> colors, int productId)
    {
        var quickReplies = colors.Select(c => new QuickReply
        {
            Title = c,
            Payload = $"SELECT_COLOR_{productId}_{c}"
        }).ToList();

        return new
        {
            text = "Vui lòng chọn màu sắc:",
            quick_replies = quickReplies
        };
    }

    public object BuildSizeSelection(List<string> sizes, int productId, string color)
    {
        var quickReplies = sizes.Select(s => new QuickReply
        {
            Title = s,
            Payload = $"SELECT_SIZE_{productId}_{color}_{s}"
        }).ToList();

        return new
        {
            text = "Vui lòng chọn kích cỡ:",
            quick_replies = quickReplies
        };
    }
}
```

### 9. Update IMessengerService
```csharp
public interface IMessengerService
{
    Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text);
    Task<SendMessageResponse> SendTemplateAsync(string recipientId, object template);
    Task<SendMessageResponse> SendQuickRepliesAsync(string recipientId, string text, List<QuickReply> quickReplies);
}
```

### 10. Update MessengerService
```csharp
public async Task<SendMessageResponse> SendTemplateAsync(string recipientId, object template)
{
    var request = new
    {
        recipient = new { id = recipientId },
        message = template
    };

    var url = $"https://graph.facebook.com/v21.0/me/messages?access_token={_options.PageAccessToken}";
    var response = await _httpClient.PostAsJsonAsync(url, request);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        _logger.LogError("Graph API error: {StatusCode} - {Error}", response.StatusCode, error);
        throw new HttpRequestException($"Graph API error: {response.StatusCode}");
    }

    var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
    return result ?? throw new InvalidOperationException("Failed to deserialize response");
}
```

### 11. Implement BrowsingStateHandler
```csharp
public class BrowsingStateHandler : IStateHandler
{
    private readonly IProductService _productService;
    private readonly ITemplateBuilder _templateBuilder;
    private readonly IMessengerService _messengerService;
    private readonly IGeminiService _geminiService;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        // Use Gemini to understand intent
        var intent = await DetectIntentAsync(message);

        if (intent.Category != null)
        {
            // Show products by category
            var products = await _productService.GetByCategoryAsync(intent.Category);
            if (products.Any())
            {
                var template = _templateBuilder.BuildProductCarousel(products);
                await _messengerService.SendTemplateAsync(context.FacebookPSID, template);
                return $"Đây là các sản phẩm {intent.Category} của chúng tôi:";
            }
        }
        else if (!string.IsNullOrEmpty(intent.SearchQuery))
        {
            // Search products
            var products = await _productService.SearchAsync(intent.SearchQuery);
            if (products.Any())
            {
                var template = _templateBuilder.BuildProductCarousel(products);
                await _messengerService.SendTemplateAsync(context.FacebookPSID, template);
                return $"Tìm thấy {products.Count} sản phẩm:";
            }
            else
            {
                return "Xin lỗi, không tìm thấy sản phẩm phù hợp. Bạn có thể thử từ khóa khác?";
            }
        }

        // Fallback to AI response
        var response = await _geminiService.SendMessageAsync(
            context.FacebookPSID,
            message,
            context.History);

        return response;
    }

    private async Task<IntentResult> DetectIntentAsync(string message)
    {
        // Simple keyword matching (can be enhanced with Gemini)
        var categories = new Dictionary<string, string[]>
        {
            ["shirts"] = new[] { "áo", "sơ mi", "thun", "polo" },
            ["pants"] = new[] { "quần", "jean", "kaki" },
            ["dresses"] = new[] { "váy", "đầm" },
            ["shoes"] = new[] { "giày", "dép" },
            ["accessories"] = new[] { "phụ kiện", "túi", "ví", "thắt lưng" }
        };

        foreach (var (category, keywords) in categories)
        {
            if (keywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return new IntentResult { Category = category };
            }
        }

        return new IntentResult { SearchQuery = message };
    }
}
```

### 12. Register Services in Program.cs
```csharp
// Product services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IVariantService, VariantService>();

// Template builder
builder.Services.AddSingleton<ITemplateBuilder, TemplateBuilder>();
```

### 13. Write Unit Tests
```csharp
[Fact]
public async Task GetByCategoryAsync_ValidCategory_ReturnsProducts()
{
    var products = await _productService.GetByCategoryAsync("shirts");
    Assert.NotEmpty(products);
    Assert.All(products, p => Assert.Equal("shirts", p.Category));
}

[Fact]
public async Task GetAvailableColorsAsync_ProductWithVariants_ReturnsColors()
{
    var colors = await _variantService.GetAvailableColorsAsync(1);
    Assert.NotEmpty(colors);
}

[Fact]
public void BuildProductCarousel_ValidProducts_ReturnsTemplate()
{
    var products = new List<ProductDto> { /* test data */ };
    var template = _templateBuilder.BuildProductCarousel(products);
    Assert.NotNull(template);
}
```

---

## Todo List

- [ ] Create ProductDto and VariantDto models
- [ ] Implement IProductService interface
- [ ] Implement ProductService with caching
- [ ] Implement IVariantService interface
- [ ] Implement VariantService with stock management
- [ ] Create Messenger template models
- [ ] Implement ITemplateBuilder interface
- [ ] Implement TemplateBuilder for all templates
- [ ] Update IMessengerService with template methods
- [ ] Update MessengerService to send templates
- [ ] Implement BrowsingStateHandler
- [ ] Implement ProductViewStateHandler
- [ ] Register all services in DI container
- [ ] Write unit tests for product services
- [ ] Write unit tests for template builder
- [ ] Integration test with real Messenger API
- [ ] Test product carousel display
- [ ] Test quick replies for color/size selection

---

## Success Criteria

- Product queries return results <50ms
- Product carousel displays correctly in Messenger
- Quick replies work for color/size selection
- Stock validation prevents overselling
- Caching reduces database load
- Unit tests pass (100% coverage)
- Integration tests pass with Messenger API
- Vietnamese text displays correctly

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Image loading slow | Medium | Medium | Use CDN, optimize image sizes |
| Stock race conditions | Medium | High | Use database transactions |
| Template rendering issues | Low | Medium | Test with real Messenger accounts |
| Large catalog performance | Low | Medium | Implement pagination, caching |

---

## Security Considerations

- Validate product IDs before queries (prevent SQL injection)
- Sanitize search queries
- Rate limit search requests per user
- Don't expose internal product data
- Log stock changes for audit trail

---

## Next Steps

After Phase 4 completion:
1. Proceed to Phase 5: Conversation Flows
2. Integrate product services into state handlers
3. Build AI prompts for product recommendations
4. Test full browsing flow end-to-end
5. Optimize product images for mobile
