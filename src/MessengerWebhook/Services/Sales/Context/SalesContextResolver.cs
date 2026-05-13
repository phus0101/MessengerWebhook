using System.Text.RegularExpressions;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Utilities;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.Sales.Context;

/// <summary>
/// Resolves sales conversation context: product resolution, VIP lookup, history recovery,
/// commercial snapshots. Pure reads + in-memory StateContext mutations only.
/// No Messenger sends, no external DB writes.
/// </summary>
public sealed class SalesContextResolver : ISalesContextResolver
{
    private readonly ICustomerIntelligenceService _customerIntelligence;
    private readonly IProductMappingService _productMapping;
    private readonly IGiftSelectionService _giftSelection;
    private readonly IFreeshipCalculator _freeshipCalculator;
    private readonly IProductGroundingService _productGrounding;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<SalesContextResolver> _logger;

    public SalesContextResolver(
        ICustomerIntelligenceService customerIntelligence,
        IProductMappingService productMapping,
        IGiftSelectionService giftSelection,
        IFreeshipCalculator freeshipCalculator,
        IProductGroundingService productGrounding,
        IGeminiService geminiService,
        ILogger<SalesContextResolver> logger)
    {
        _customerIntelligence = customerIntelligence;
        _productMapping = productMapping;
        _giftSelection = giftSelection;
        _freeshipCalculator = freeshipCalculator;
        _productGrounding = productGrounding;
        _geminiService = geminiService;
        _logger = logger;
    }

    // ── VIP ────────────────────────────────────────────────────────────────

    public async Task<VipProfile?> GetVipProfileAsync(StateContext ctx)
    {
        // Read-only lookup — do NOT use GetOrCreateAsync here (that causes a DB write).
        // New customers return null; callers treat null as "new customer" and apply default behavior.
        var customer = await _customerIntelligence.GetExistingAsync(
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"));

        return customer == null ? null : await _customerIntelligence.GetVipProfileAsync(customer);
    }

    // ── Product resolution ─────────────────────────────────────────────────

    public async Task<List<Product>> GetActiveSelectedProductsAsync(StateContext ctx)
    {
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        var active = new List<Product>();
        foreach (var code in selectedCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var product = await _productMapping.GetActiveProductByCodeAsync(code);
            if (product != null) active.Add(product);
        }
        return active;
    }

    public async Task<Product?> GetActiveProductOrResolveAsync(StateContext ctx, string message)
    {
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (selectedCodes.Count > 0)
        {
            var activeProduct = await _productMapping.GetActiveProductByCodeAsync(selectedCodes[0]);
            if (activeProduct != null)
            {
                var directProduct = await _productMapping.GetProductByMessageAsync(message);
                if (directProduct == null ||
                    string.Equals(directProduct.Code, activeProduct.Code, StringComparison.OrdinalIgnoreCase) ||
                    !ShouldSwitchActiveProduct(message, activeProduct, directProduct))
                {
                    return activeProduct;
                }
            }
        }
        return await ResolveCurrentProductAsync(ctx, message);
    }

    public async Task<Product?> ResolveCurrentProductAsync(StateContext ctx, string message)
    {
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (selectedCodes.Count > 0)
        {
            var activeProduct = await _productMapping.GetActiveProductByCodeAsync(selectedCodes[0]);
            if (activeProduct != null)
            {
                var directProduct = await _productMapping.GetProductByMessageAsync(message);
                if (directProduct != null &&
                    !string.Equals(directProduct.Code, activeProduct.Code, StringComparison.OrdinalIgnoreCase) &&
                    ShouldSwitchActiveProduct(message, activeProduct, directProduct))
                {
                    await ApplyResolvedProductAsync(ctx, directProduct, "explicit-switch");
                    return directProduct;
                }
                return activeProduct;
            }
        }

        var matchedProduct = await _productMapping.GetProductByMessageAsync(message);
        if (matchedProduct != null)
        {
            await ApplyResolvedProductAsync(ctx, matchedProduct, "direct-message");
            return matchedProduct;
        }

        await TryExtractProductFromHistoryAsync(ctx);
        selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        return selectedCodes.Count == 0
            ? null
            : await _productMapping.GetActiveProductByCodeAsync(selectedCodes[0]);
    }

    public async Task ApplyResolvedProductAsync(StateContext ctx, Product product, string source)
    {
        ctx.SetData("selectedProductCodes", new List<string> { product.Code });
        ctx.SetData("lastResolvedProductCode", product.Code);
        ctx.SetData("lastResolvedProductSource", source);
        var gift = await _giftSelection.SelectGiftForProductAsync(product.Code);
        var shippingFee = _freeshipCalculator.CalculateShippingFee(new List<string> { product.Code });
        ctx.SetData("selectedGiftCode", gift?.Code ?? string.Empty);
        ctx.SetData("selectedGiftName", gift?.Name ?? string.Empty);
        ctx.SetData("shippingFee", shippingFee);
    }

    public async Task SyncActiveProductPolicyContextAsync(StateContext ctx, string productCode)
    {
        var gift = await _giftSelection.SelectGiftForProductAsync(productCode);
        ctx.SetData("selectedGiftCode", gift?.Code ?? string.Empty);
        ctx.SetData("selectedGiftName", gift?.Name ?? string.Empty);
        ctx.SetData("shippingFee", _freeshipCalculator.CalculateShippingFee(new List<string> { productCode }));
    }

    public async Task RefreshSelectedProductPolicyContextAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        var productCode = product?.Code
            ?? (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(productCode)) return;
        ctx.SetData("selectedProductCodes", new List<string> { productCode });
        await SyncActiveProductPolicyContextAsync(ctx, productCode);
    }

    // ── History recovery ───────────────────────────────────────────────────

    public async Task TryExtractProductFromHistoryAsync(StateContext ctx, string? currentMessage = null)
    {
        if ((ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0)
        {
            _logger.LogDebug("Skip history recovery — active product exists");
            return;
        }

        _logger.LogInformation("Extracting product from conversation history");

        var recentMessages = (ctx.GetData<List<AiConversationMessage>>("conversationHistory")
            ?? new List<AiConversationMessage>()).TakeLast(10).ToList();

        var hasNumberedSelection = ExtractRelatedSuggestionSelectionNumber(currentMessage).HasValue;
        if (await TryResolveNumberedSuggestionSelectionAsync(ctx, currentMessage) != null || hasNumberedSelection)
            return;

        var userCandidates = await CollectHistoryProductCandidatesAsync(recentMessages, "user");
        var assistantCandidates = await CollectHistoryProductCandidatesAsync(recentMessages, "assistant");
        var preferredCandidates = userCandidates.Count > 0 ? userCandidates : assistantCandidates;
        var preferredRole = userCandidates.Count > 0 ? "user" : "assistant";

        if (preferredCandidates.Count == 0)
        {
            _logger.LogWarning("No product found in conversation history");
            return;
        }

        var resolvedCandidate = preferredCandidates.Count == 1
            ? preferredCandidates[0]
            : await ResolveAmbiguousHistoryProductCandidateAsync(ctx, recentMessages, preferredCandidates, preferredRole)
              ?? preferredCandidates[0];

        _logger.LogInformation(
            "Extracted {Product} ({Code}) from {Role} history for PSID: {PSID}",
            resolvedCandidate.Product.Name, resolvedCandidate.Product.Code,
            resolvedCandidate.Role, ctx.FacebookPSID);

        await ApplyResolvedProductAsync(ctx, resolvedCandidate.Product, "history-recovery");
    }

    public async Task<Product?> TryResolveNumberedSuggestionSelectionAsync(StateContext ctx, string? currentMessage)
    {
        var recentMessages = (ctx.GetData<List<AiConversationMessage>>("conversationHistory")
            ?? new List<AiConversationMessage>()).TakeLast(10).ToList();
        var numberedSuggestion = await ResolveNumberedAssistantSuggestionAsync(recentMessages, currentMessage);
        if (numberedSuggestion == null) return null;

        _logger.LogInformation(
            "Extracted {Product} ({Code}) from numbered suggestion for PSID: {PSID}",
            numberedSuggestion.Name, numberedSuggestion.Code, ctx.FacebookPSID);
        await ApplyResolvedProductAsync(ctx, numberedSuggestion, "numbered-suggestion");
        return numberedSuggestion;
    }

    private async Task<Product?> ResolveNumberedAssistantSuggestionAsync(
        List<AiConversationMessage> recentMessages, string? currentMessage)
    {
        var selectedNumber = ExtractRelatedSuggestionSelectionNumber(currentMessage);
        if (!selectedNumber.HasValue) return null;

        foreach (var msg in recentMessages
                     .Where(x => string.Equals(x.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                     .Reverse())
        {
            var numberedLines = msg.Content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsNumberedSuggestionLine)
                .ToList();

            if (numberedLines.Count == 0) continue;

            var selectedLine = numberedLines.FirstOrDefault(line =>
                line.StartsWith($"{selectedNumber.Value})", StringComparison.Ordinal));
            return selectedLine == null ? null : await _productMapping.GetProductByMessageAsync(selectedLine);
        }
        return null;
    }

    public async Task<List<HistoryProductCandidate>> CollectHistoryProductCandidatesAsync(
        List<AiConversationMessage> recentMessages, string role)
    {
        var candidates = new List<HistoryProductCandidate>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var msg in recentMessages
                     .Where(x => string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase))
                     .Reverse())
        {
            var product = await _productMapping.GetProductByMessageAsync(msg.Content);
            if (product == null || !seenCodes.Add(product.Code)) continue;
            candidates.Add(new HistoryProductCandidate(product, msg.Role, msg.Content));
        }
        return candidates;
    }

    public async Task<HistoryProductCandidate?> ResolveAmbiguousHistoryProductCandidateAsync(
        StateContext ctx,
        List<AiConversationMessage> recentMessages,
        List<HistoryProductCandidate> candidates,
        string preferredRole)
    {
        var candidateProducts = candidates
            .Select(c => new GroundedProduct(c.Product.Id, c.Product.Code, c.Product.Name,
                c.Product.Category.ToString(), c.Product.BasePrice))
            .ToList();
        var sanitizedMessages = _productGrounding.SanitizeAssistantHistory(recentMessages, candidateProducts).ToList();
        var candidateSummary = string.Join(", ", candidates.Select(x => $"{x.Product.Name} ({x.Product.Code})"));
        var historySummary = string.Join("\n", sanitizedMessages.Select(x => $"{x.Role}: {x.Content}"));
        var prompt = $"""
Chọn đúng 1 mã sản phẩm khách đang muốn mua nhất từ lịch sử chat gần đây.
Ưu tiên message mới hơn và ưu tiên message từ user hơn assistant.
Chỉ trả về đúng 1 product code trong danh sách này: {string.Join(", ", candidates.Select(x => x.Product.Code))}
Nếu chưa chắc, vẫn chọn mã có khả năng cao nhất theo ưu tiên trên.
Preferred role: {preferredRole}
Candidates: {candidateSummary}
History:
{historySummary}
""";

        var aiResponse = await _geminiService.SendMessageAsync(
            ctx.FacebookPSID, prompt, sanitizedMessages,
            AI.Models.GeminiModelType.FlashLite,
            cancellationToken: CancellationToken.None);

        var matchedCandidate = candidates.FirstOrDefault(x =>
            aiResponse.Contains(x.Product.Code, StringComparison.OrdinalIgnoreCase));
        if (matchedCandidate != null)
            _logger.LogInformation(
                "Resolved ambiguous history product via AI for PSID: {PSID}, ProductCode: {Code}",
                ctx.FacebookPSID, matchedCandidate.Product.Code);
        return matchedCandidate;
    }

    // ── Commercial snapshots ───────────────────────────────────────────────

    public async Task<CommercialFactSnapshot?> BuildCommercialFactSnapshotAsync(StateContext ctx, Product product)
    {
        var selectedVariantId = ctx.GetData<string>("selectedVariantId");
        var selectedVariant = product.Variants.FirstOrDefault(v =>
            string.Equals(v.Id, selectedVariantId, StringComparison.OrdinalIgnoreCase));
        var gift = await _giftSelection.SelectGiftForProductAsync(product.Code);
        return CommercialFactSnapshot.Create(product, selectedVariant, gift, ctx.GetData<decimal?>("shippingFee"), false);
    }

    public async Task<CommercialFactSnapshot?> BuildCommercialFactSnapshotForPolicyAsync(StateContext ctx, Product product)
    {
        await RefreshSelectedProductPolicyContextAsync(ctx, product.Code);
        var baseSnapshot = await BuildCommercialFactSnapshotAsync(ctx, product);
        if (baseSnapshot == null) return null;
        return baseSnapshot with { ShippingFee = null, ShippingConfirmed = false, IsFreeship = null };
    }

    // ── Selection detection ────────────────────────────────────────────────

    public bool IsRelatedSuggestionSelection(string message)
        => ExtractRelatedSuggestionSelectionNumber(message).HasValue;

    public int? ExtractRelatedSuggestionSelectionNumber(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var normalized = SalesTextHelper.NormalizeForMatching(message);

        var match = Regex.Match(normalized,
            @"\b(san pham|mon|lua chon)\s*(so\s*)?(?<number>[1-9]|1\d|20)\b",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            match = Regex.Match(normalized,
                @"\bchon\s*(san pham|mon|lua chon)?\s*(so\s*)?(?<number>[1-9]|1\d|20)\b",
                RegexOptions.CultureInvariant);

        if (match.Success && int.TryParse(match.Groups["number"].Value, out var n)) return n;

        // Pure number-only message
        match = Regex.Match(normalized, @"^(?<number>[1-9]|1\d|20)$", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["number"].Value, out n) ? n : null;
    }

    // ── Private static product-switch helpers ──────────────────────────────

    private static bool ShouldSwitchActiveProduct(string message, Product activeProduct, Product directProduct)
    {
        var normalized = SalesTextHelper.NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var hasReplacement = normalized.Contains("doi sang", StringComparison.Ordinal)
            || normalized.Contains("chuyen sang", StringComparison.Ordinal)
            || normalized.Contains("thay cho", StringComparison.Ordinal)
            || normalized.Contains("khong lay", StringComparison.Ordinal)
            || normalized.Contains("khong mua", StringComparison.Ordinal)
            || normalized.Contains("lay thay", StringComparison.Ordinal)
            || normalized.Contains("chot lai", StringComparison.Ordinal);

        if (hasReplacement) return true;

        var hasCommitment = normalized.Contains("mua ", StringComparison.Ordinal)
            || normalized.Contains("muon mua", StringComparison.Ordinal)
            || normalized.Contains("chot", StringComparison.Ordinal)
            || normalized.Contains("lay ", StringComparison.Ordinal)
            || normalized.Contains("chot don", StringComparison.Ordinal)
            || normalized.Contains("len don", StringComparison.Ordinal);

        if (!hasCommitment) return false;
        if (HasPolicyOrComparisonSignal(normalized)) return false;

        var mentionsActive = ReferencesProduct(normalized, activeProduct);
        var mentionsDirect = ReferencesProduct(normalized, directProduct);
        if (!mentionsDirect || mentionsActive) return false;

        return HasDirectProductCommitment(normalized, directProduct);
    }

    private static bool HasPolicyOrComparisonSignal(string normalizedMessage)
        => normalizedMessage.Contains("freeship", StringComparison.Ordinal)
            || normalizedMessage.Contains("free ship", StringComparison.Ordinal)
            || normalizedMessage.Contains("phi ship", StringComparison.Ordinal)
            || normalizedMessage.Contains("van chuyen", StringComparison.Ordinal)
            || normalizedMessage.Contains("khuyen mai", StringComparison.Ordinal)
            || normalizedMessage.Contains("uu dai", StringComparison.Ordinal)
            || normalizedMessage.Contains("giam gia", StringComparison.Ordinal)
            || normalizedMessage.Contains("qua tang", StringComparison.Ordinal)
            || normalizedMessage.Contains("so voi", StringComparison.Ordinal)
            || normalizedMessage.Contains("voi", StringComparison.Ordinal)
            || normalizedMessage.Contains("hay", StringComparison.Ordinal)
            || normalizedMessage.Contains("con", StringComparison.Ordinal)
            || normalizedMessage.Contains("cung", StringComparison.Ordinal)
            || normalizedMessage.Contains("khong em", StringComparison.Ordinal)
            || (normalizedMessage.Contains("khong", StringComparison.Ordinal)
                && !normalizedMessage.Contains("khong lay", StringComparison.Ordinal)
                && !normalizedMessage.Contains("khong mua", StringComparison.Ordinal));

    private static bool ReferencesProduct(string normalizedMessage, Product product)
        => GetProductAliases(product).Any(alias => normalizedMessage.Contains(alias, StringComparison.Ordinal));

    private static bool HasDirectProductCommitment(string normalizedMessage, Product product)
        => GetProductAliases(product).Any(alias =>
            normalizedMessage.Contains($"mua {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"lay {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"chot {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"len don {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"lay {alias} nha", StringComparison.Ordinal)
            || normalizedMessage.Contains($"lay {alias} nhe", StringComparison.Ordinal));

    private static List<string> GetProductAliases(Product product)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal)
        {
            SalesTextHelper.NormalizeForMatching(product.Code).Replace("_", " ", StringComparison.Ordinal),
            SalesTextHelper.NormalizeForMatching(product.Name)
        };
        foreach (var token in SalesTextHelper.NormalizeForMatching(product.Name)
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            aliases.Add(token);
        return aliases.Where(a => a.Length >= 2).OrderByDescending(a => a.Length).ToList();
    }

    private static bool IsNumberedSuggestionLine(string line)
        => Regex.IsMatch(line, @"^([1-9]|1\d|20)\)", RegexOptions.CultureInvariant);
}
