using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.ProductGrounding;

public interface IProductGroundingService
{
    GroundedProductContext BuildContext(string customerMessage, IEnumerable<Product> activeProducts, IEnumerable<GroundedProduct> ragProducts);
    IReadOnlyList<MessengerWebhook.Services.AI.Models.ConversationMessage> SanitizeAssistantHistory(
        IEnumerable<MessengerWebhook.Services.AI.Models.ConversationMessage> history,
        IReadOnlyCollection<GroundedProduct> allowedProducts);
}

public class ProductGroundingService : IProductGroundingService
{
    public const string FallbackReply = "Dạ hiện em chưa tìm thấy dữ liệu sản phẩm phù hợp trong catalog để báo chính xác ạ. Chị cho em tên hoặc mã sản phẩm cụ thể, hoặc để em chuyển bạn hỗ trợ kiểm tra lại giúp mình nha.";

    private readonly IProductNeedDetector _needDetector;
    private readonly IProductMentionDetector _mentionDetector;

    public ProductGroundingService(IProductNeedDetector needDetector, IProductMentionDetector mentionDetector)
    {
        _needDetector = needDetector;
        _mentionDetector = mentionDetector;
    }

    public GroundedProductContext BuildContext(string customerMessage, IEnumerable<Product> activeProducts, IEnumerable<GroundedProduct> ragProducts)
    {
        var allowedProducts = activeProducts
            .Select(product => new GroundedProduct(product.Id, product.Code, product.Name, product.Category.ToString(), product.BasePrice))
            .Concat(ragProducts)
            .Where(product => !string.IsNullOrWhiteSpace(product.Name) || !string.IsNullOrWhiteSpace(product.Code))
            .GroupBy(product => BuildDeduplicationKey(product), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return new GroundedProductContext(
            _needDetector.RequiresProductGrounding(customerMessage),
            allowedProducts,
            FallbackReply);
    }

    public IReadOnlyList<MessengerWebhook.Services.AI.Models.ConversationMessage> SanitizeAssistantHistory(
        IEnumerable<MessengerWebhook.Services.AI.Models.ConversationMessage> history,
        IReadOnlyCollection<GroundedProduct> allowedProducts)
    {
        return history
            .Where(message => message.Role != "assistant" || IsAllowedAssistantMessage(message.Content, allowedProducts))
            .ToList();
    }

    private bool IsAllowedAssistantMessage(string content, IReadOnlyCollection<GroundedProduct> allowedProducts)
    {
        var mentions = _mentionDetector.ExtractProductMentions(content);
        return mentions.Count == 0 || mentions.All(mention => IsAllowedMention(mention, allowedProducts));
    }

    private static bool IsAllowedMention(string mention, IEnumerable<GroundedProduct> allowedProducts)
    {
        return allowedProducts.Any(product =>
            ContainsEquivalent(mention, product.Name) ||
            ContainsProductCode(mention, product.Code));
    }

    private static bool ContainsEquivalent(string candidate, string allowedValue)
    {
        return !string.IsNullOrWhiteSpace(candidate) &&
               !string.IsNullOrWhiteSpace(allowedValue) &&
               string.Equals(NormalizeProductName(candidate), NormalizeProductName(allowedValue), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsProductCode(string candidate, string allowedCode)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(allowedCode))
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(candidate, $@"(?<![\p{{L}}\p{{M}}0-9]){System.Text.RegularExpressions.Regex.Escape(allowedCode)}(?![\p{{L}}\p{{M}}0-9])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string NormalizeProductName(string value)
    {
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildDeduplicationKey(GroundedProduct product)
    {
        if (!string.IsNullOrWhiteSpace(product.Id))
        {
            return $"id:{product.Id}";
        }

        if (!string.IsNullOrWhiteSpace(product.Code))
        {
            return $"code:{product.Code}";
        }

        return $"name:{product.Name}";
    }
}
