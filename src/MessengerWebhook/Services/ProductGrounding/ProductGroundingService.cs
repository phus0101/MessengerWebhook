using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.Tenants;

namespace MessengerWebhook.Services.ProductGrounding;

public interface IProductGroundingService
{
    GroundedProductContext BuildContext(string customerMessage, IEnumerable<Product> activeProducts, IEnumerable<GroundedProduct> ragProducts);
    Task<GroundedProductContext> BuildContextWithRelatedSuggestionsAsync(
        string customerMessage,
        IEnumerable<Product> activeProducts,
        IEnumerable<GroundedProduct> ragProducts,
        CancellationToken cancellationToken = default);
    IReadOnlyList<MessengerWebhook.Services.AI.Models.ConversationMessage> SanitizeAssistantHistory(
        IEnumerable<MessengerWebhook.Services.AI.Models.ConversationMessage> history,
        IReadOnlyCollection<GroundedProduct> allowedProducts);
}

public class ProductGroundingService : IProductGroundingService
{
    public const string FallbackReply = "Dạ hiện em chưa tìm thấy dữ liệu sản phẩm phù hợp trong catalog để báo chính xác ạ. Chị cho em tên hoặc mã sản phẩm cụ thể, hoặc để em chuyển bạn hỗ trợ kiểm tra lại giúp mình nha.";

    private const int MaxRelatedSuggestionCount = 3;

    private static readonly RelatedTermGroup[] RelatedTermGroups =
    {
        new("mặt nạ", "mat na", ProductCategory.Cosmetics, new[] { "mặt nạ", "mat na" }),
        new("sữa rửa mặt", "sua rua mat", ProductCategory.Cosmetics, new[] { "sữa rửa mặt", "sua rua mat" }),
        new("chống nắng", "chong nang", ProductCategory.Cosmetics, new[] { "chống nắng", "chong nang", "kem chống nắng", "kem chong nang" }),
        new("tẩy trang", "tay trang", ProductCategory.Cosmetics, new[] { "tẩy trang", "tay trang" }),
        new("serum", "serum", ProductCategory.Cosmetics, new[] { "serum", "tinh chất", "tinh chat" }),
        new("toner", "toner", ProductCategory.Cosmetics, new[] { "toner", "nước hoa hồng", "nuoc hoa hong" }),
        new("kem", "kem", ProductCategory.Cosmetics, new[] { "kem" })
    };

    private static readonly string[] BenefitTerms =
    {
        "dưỡng ẩm", "duong am", "cấp ẩm", "cap am", "phục hồi", "phuc hoi", "da khô", "da kho",
        "khô", "kho", "thiếu ẩm", "thieu am", "nám", "nam", "tàn nhang", "tan nhang",
        "mụn", "mun", "dầu", "dau", "sáng da", "sang da"
    };

    private readonly IProductNeedDetector _needDetector;
    private readonly IProductMentionDetector _mentionDetector;
    private readonly IProductRepository? _productRepository;
    private readonly ITenantContext? _tenantContext;

    public ProductGroundingService(IProductNeedDetector needDetector, IProductMentionDetector mentionDetector)
        : this(needDetector, mentionDetector, null, null)
    {
    }

    public ProductGroundingService(
        IProductNeedDetector needDetector,
        IProductMentionDetector mentionDetector,
        IProductRepository? productRepository,
        ITenantContext? tenantContext)
    {
        _needDetector = needDetector;
        _mentionDetector = mentionDetector;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public GroundedProductContext BuildContext(string customerMessage, IEnumerable<Product> activeProducts, IEnumerable<GroundedProduct> ragProducts)
    {
        var allowedProducts = BuildAllowedProducts(activeProducts, ragProducts);

        return new GroundedProductContext(
            _needDetector.RequiresProductGrounding(customerMessage),
            allowedProducts,
            FallbackReply);
    }

    public async Task<GroundedProductContext> BuildContextWithRelatedSuggestionsAsync(
        string customerMessage,
        IEnumerable<Product> activeProducts,
        IEnumerable<GroundedProduct> ragProducts,
        CancellationToken cancellationToken = default)
    {
        var allowedProducts = BuildAllowedProducts(activeProducts, ragProducts);
        var requiresGrounding = _needDetector.RequiresProductGrounding(customerMessage);
        if (!requiresGrounding || allowedProducts.Count > 0 || _productRepository == null || _tenantContext?.TenantId == null)
        {
            return new GroundedProductContext(requiresGrounding, allowedProducts, FallbackReply);
        }

        var criteria = ExtractRelatedCriteria(customerMessage);
        if (!criteria.HasCriteria)
        {
            return new GroundedProductContext(requiresGrounding, allowedProducts, FallbackReply);
        }

        var relatedProducts = await _productRepository.GetActiveRelatedAsync(
            _tenantContext.TenantId.Value,
            criteria.Category,
            criteria.Terms,
            MaxRelatedSuggestionCount,
            cancellationToken);

        var suggestions = relatedProducts
            .Select(product => new GroundedProduct(product.Id, product.Code, product.Name, product.Category.ToString(), product.BasePrice))
            .Where(product => !string.IsNullOrWhiteSpace(product.Name) || !string.IsNullOrWhiteSpace(product.Code))
            .GroupBy(product => BuildDeduplicationKey(product), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(MaxRelatedSuggestionCount)
            .ToList();

        if (suggestions.Count == 0)
        {
            return new GroundedProductContext(requiresGrounding, allowedProducts, FallbackReply);
        }

        return new GroundedProductContext(
            requiresGrounding,
            allowedProducts,
            FallbackReply,
            suggestions,
            BuildRelatedSuggestionReply(customerMessage, criteria.DisplayName, suggestions));
    }

    public IReadOnlyList<MessengerWebhook.Services.AI.Models.ConversationMessage> SanitizeAssistantHistory(
        IEnumerable<MessengerWebhook.Services.AI.Models.ConversationMessage> history,
        IReadOnlyCollection<GroundedProduct> allowedProducts)
    {
        return history
            .Where(message => message.Role != "assistant" || IsAllowedAssistantMessage(message.Content, allowedProducts))
            .ToList();
    }

    internal static RelatedProductCriteria ExtractRelatedCriteria(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return RelatedProductCriteria.Empty;
        }

        var matchedCategory = RelatedTermGroups.FirstOrDefault(group => ContainsAny(message, group.MatchTerms));
        if (matchedCategory == null)
        {
            return RelatedProductCriteria.Empty;
        }

        var terms = new List<string> { matchedCategory.SearchTerm };
        terms.AddRange(BenefitTerms.Where(term => message.Contains(term, StringComparison.OrdinalIgnoreCase)));

        return new RelatedProductCriteria(
            matchedCategory.Category,
            terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            matchedCategory.DisplayName);
    }

    private static List<GroundedProduct> BuildAllowedProducts(IEnumerable<Product> activeProducts, IEnumerable<GroundedProduct> ragProducts)
    {
        return activeProducts
            .Select(product => new GroundedProduct(product.Id, product.Code, product.Name, product.Category.ToString(), product.BasePrice))
            .Concat(ragProducts)
            .Where(product => !string.IsNullOrWhiteSpace(product.Name) || !string.IsNullOrWhiteSpace(product.Code))
            .GroupBy(product => BuildDeduplicationKey(product), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static string BuildRelatedSuggestionReply(string customerMessage, string displayName, IReadOnlyList<GroundedProduct> suggestions)
    {
        var lines = new List<string>
        {
            "Dạ hiện em chưa thấy đúng mã/tên cụ thể trong catalog. Em gợi ý vài lựa chọn có dữ liệu trên hệ thống:"
        };

        for (var index = 0; index < suggestions.Count; index++)
        {
            var product = suggestions[index];
            var code = string.IsNullOrWhiteSpace(product.Code) ? string.Empty : $" ({product.Code})";
            var price = product.Price.HasValue ? $" - {FormatPrice(product.Price.Value)}" : string.Empty;
            lines.Add($"{index + 1}) {product.Name}{code}{price}");
        }

        lines.Add("Chị muốn xem sản phẩm nào ạ?");
        return string.Join(Environment.NewLine, lines);
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
        return ProductNameNormalizer.Equivalent(candidate, allowedValue);
    }

    private static bool ContainsProductCode(string candidate, string allowedCode)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(allowedCode))
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(candidate, $@"(?<![\p{{L}}\p{{M}}0-9]){System.Text.RegularExpressions.Regex.Escape(allowedCode)}(?![\p{{L}}\p{{M}}0-9])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool ContainsAny(string value, IEnumerable<string> phrases)
    {
        return phrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatPrice(decimal price)
    {
        return $"{price:N0}đ";
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

    private sealed record RelatedTermGroup(string DisplayName, string SearchTerm, ProductCategory Category, IReadOnlyList<string> MatchTerms);
}

public record RelatedProductCriteria(ProductCategory? Category, IReadOnlyCollection<string> Terms, string DisplayName)
{
    public static RelatedProductCriteria Empty { get; } = new(null, Array.Empty<string>(), string.Empty);
    public bool HasCriteria => Category.HasValue || Terms.Count > 0;
}
