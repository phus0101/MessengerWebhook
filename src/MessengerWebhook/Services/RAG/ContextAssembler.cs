using MessengerWebhook.Data;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MessengerWebhook.Services.RAG;

/// <summary>
/// Formats products into Vietnamese LLM context with token estimation
/// </summary>
public class ContextAssembler : IContextAssembler
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ContextAssembler> _logger;

    public ContextAssembler(
        MessengerBotDbContext dbContext,
        ILogger<ContextAssembler> logger)
        : this(dbContext, new NullTenantContext(), logger)
    {
    }

    public ContextAssembler(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<ContextAssembler> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AssembledRAGContext> AssembleContextAsync(
        List<string> productIds,
        bool includeDetailedInfo = false,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return CreateEmptyContext();
        }

        if (!_tenantContext.TenantId.HasValue)
        {
            _logger.LogWarning("Context assembly skipped because tenant context is not resolved");
            return CreateEmptyContext();
        }

        var tenantId = _tenantContext.TenantId.Value;
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var sortedProducts = productIds
            .Select(id => products.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null)
            .ToList();

        if (sortedProducts.Count == 0)
        {
            _logger.LogWarning("No products found in database for IDs: {ProductIds}", string.Join(", ", productIds));
            return CreateEmptyContext();
        }

        var context = new StringBuilder();
        context.AppendLine("Sản phẩm liên quan:");
        context.AppendLine();

        for (int i = 0; i < sortedProducts.Count; i++)
        {
            var product = sortedProducts[i];
            context.AppendLine($"{i + 1}. {product!.Name}");
            context.AppendLine($"   Mã: {product.Code}");
            context.AppendLine("   Giá và chính sách bán hàng cần xác nhận theo dữ liệu runtime hiện tại.");

            if (!string.IsNullOrEmpty(product.Description))
            {
                var desc = includeDetailedInfo
                    ? product.Description
                    : (product.Description.Length > 200
                        ? product.Description.Substring(0, 197) + "..."
                        : product.Description);
                context.AppendLine($"   {desc}");
            }

            if (includeDetailedInfo)
            {
                AppendDetailedInfo(context, product);
            }

            context.AppendLine();
        }

        var contextStr = context.ToString();
        var estimatedTokens = EstimateTokens(contextStr);

        _logger.LogInformation(
            "Assembled context for {Count} products, ~{Tokens} tokens (detailed: {Detailed})",
            sortedProducts.Count,
            estimatedTokens,
            includeDetailedInfo);

        var groundedProducts = sortedProducts
            .Select(product => new GroundedProduct(
                product!.Id,
                product.Code,
                product.Name,
                product.Category.ToString(),
                product.BasePrice))
            .ToList();

        return new AssembledRAGContext(
            contextStr,
            groundedProducts.Select(product => product.Id).ToList(),
            groundedProducts);
    }

    private void AppendDetailedInfo(StringBuilder context, Data.Entities.Product product)
    {
        if (!string.IsNullOrEmpty(product.IngredientsJson))
        {
            try
            {
                var ingredients = System.Text.Json.JsonSerializer.Deserialize<string[]>(product.IngredientsJson);
                if (ingredients != null && ingredients.Length > 0)
                {
                    context.AppendLine($"   Thành phần: {string.Join(", ", ingredients)}");
                }
            }
            catch { /* Ignore JSON parse errors */ }
        }

        if (!string.IsNullOrEmpty(product.SkinTypesJson))
        {
            try
            {
                var skinTypes = System.Text.Json.JsonSerializer.Deserialize<string[]>(product.SkinTypesJson);
                if (skinTypes != null && skinTypes.Length > 0)
                {
                    context.AppendLine($"   Phù hợp với da: {string.Join(", ", skinTypes)}");
                }
            }
            catch { /* Ignore JSON parse errors */ }
        }

        if (!string.IsNullOrEmpty(product.SkinConcernsJson))
        {
            try
            {
                var concerns = System.Text.Json.JsonSerializer.Deserialize<string[]>(product.SkinConcernsJson);
                if (concerns != null && concerns.Length > 0)
                {
                    context.AppendLine($"   Giải quyết vấn đề: {string.Join(", ", concerns)}");
                }
            }
            catch { /* Ignore JSON parse errors */ }
        }

        if (product.pH.HasValue)
        {
            context.AppendLine($"   pH: {product.pH.Value:F1}");
        }

        if (!string.IsNullOrEmpty(product.Texture))
        {
            context.AppendLine($"   Kết cấu: {product.Texture}");
        }

        if (!string.IsNullOrEmpty(product.ContraindicationsJson))
        {
            try
            {
                var contraindications = System.Text.Json.JsonSerializer.Deserialize<string[]>(product.ContraindicationsJson);
                if (contraindications != null && contraindications.Length > 0)
                {
                    context.AppendLine($"   Chống chỉ định: {string.Join(", ", contraindications)}");
                }
            }
            catch { /* Ignore JSON parse errors */ }
        }
    }

    private static AssembledRAGContext CreateEmptyContext()
    {
        return new AssembledRAGContext("Không tìm thấy sản phẩm phù hợp.", new List<string>(), new List<GroundedProduct>());
    }

    private int EstimateTokens(string text)
    {
        // Rough estimate: 1 token ≈ 4 characters for Vietnamese
        return text.Length / 4;
    }
}
