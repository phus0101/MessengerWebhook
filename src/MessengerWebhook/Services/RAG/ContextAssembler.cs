using MessengerWebhook.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MessengerWebhook.Services.RAG;

/// <summary>
/// Formats products into Vietnamese LLM context with token estimation
/// </summary>
public class ContextAssembler : IContextAssembler
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ILogger<ContextAssembler> _logger;

    public ContextAssembler(
        MessengerBotDbContext dbContext,
        ILogger<ContextAssembler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> AssembleContextAsync(
        List<string> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return "Không tìm thấy sản phẩm phù hợp.";
        }

        // Load products from database
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Sort by original order (relevance from search)
        var sortedProducts = productIds
            .Select(id => products.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null)
            .ToList();

        if (sortedProducts.Count == 0)
        {
            _logger.LogWarning("No products found in database for IDs: {ProductIds}", string.Join(", ", productIds));
            return "Không tìm thấy sản phẩm phù hợp.";
        }

        // Format context
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
                // Truncate description if too long (max 200 chars)
                var desc = product.Description.Length > 200
                    ? product.Description.Substring(0, 197) + "..."
                    : product.Description;
                context.AppendLine($"   {desc}");
            }

            context.AppendLine();
        }

        var contextStr = context.ToString();
        var estimatedTokens = EstimateTokens(contextStr);

        _logger.LogInformation(
            "Assembled context for {Count} products, ~{Tokens} tokens",
            sortedProducts.Count,
            estimatedTokens);

        return contextStr;
    }

    private int EstimateTokens(string text)
    {
        // Rough estimate: 1 token ≈ 4 characters for Vietnamese
        return text.Length / 4;
    }
}
