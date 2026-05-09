using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Utilities;

namespace MessengerWebhook.Services.Sales.Contact;

/// <summary>
/// Evaluates contact confirmation state and builds contact-related replies.
/// Extracted from SalesStateHandlerBase (R-03). Pure reads + in-memory StateContext mutations only.
/// </summary>
public sealed class ContactConfirmationFlow : IContactConfirmationFlow
{
    private readonly ISalesContextResolver _contextResolver;
    private readonly ISalesPromptBuilder _promptBuilder;

    public ContactConfirmationFlow(
        ISalesContextResolver contextResolver,
        ISalesPromptBuilder promptBuilder)
    {
        _contextResolver = contextResolver;
        _promptBuilder = promptBuilder;
    }

    // ── State predicate ────────────────────────────────────────────────────────

    /// <summary>
    /// True when both contactNeedsConfirmation=true AND pendingContactQuestion="confirm_old_contact".
    /// Both flags are required — contactNeedsConfirmation alone doesn't mean we're in the confirm flow.
    /// </summary>
    private static bool IsAwaitingOldContactConfirmation(StateContext ctx)
        => ctx.GetData<bool?>("contactNeedsConfirmation") == true
            && string.Equals(
                ctx.GetData<string>("pendingContactQuestion"),
                "confirm_old_contact",
                StringComparison.OrdinalIgnoreCase);

    // ── Message content checks (pure) ─────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsContactMemoryQuestion(string message)
        => ContainsAnyPhrase(message,
            "có thông tin của chị chưa", "co thong tin cua chi chua",
            "em có thông tin của chị chưa", "em co thong tin cua chi chua",
            "có số của chị chưa", "co so cua chi chua",
            "có địa chỉ của chị chưa", "co dia chi cua chi chua",
            "em có số điện thoại của chị chưa", "em co so dien thoai cua chi chua");

    /// <inheritdoc/>
    public bool IsPendingClarificationQuestion(StateContext ctx, string message)
        => IsAwaitingOldContactConfirmation(ctx)
            && ContainsAnyPhrase(message,
                "thông tin nào", "thong tin nao",
                "thông tin gì", "thong tin gi",
                "xác nhận thông tin nào", "xac nhan thong tin nao");

    /// <inheritdoc/>
    public bool IsGenericBuyContinuationPendingConfirmation(StateContext ctx, string message)
        => IsAwaitingOldContactConfirmation(ctx) && IsGenericBuyContinuation(message);

    // ── Reply builders ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string?> BuildContactMemoryReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        var productName = product?.Name;
        var hasPhone = !string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"));
        var hasAddress = !string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"));
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;

        if (hasPhone && hasAddress)
        {
            if (needsConfirmation)
            {
                var phone = ctx.GetData<string>("customerPhone");
                var address = ctx.GetData<string>("shippingAddress");
                return string.IsNullOrWhiteSpace(productName)
                    ? $"Dạ em đang có thông tin cũ của chị rồi ạ. Chị giúp em xác nhận lại SĐT {phone} và địa chỉ {address} còn dùng đúng không ạ?"
                    : $"Dạ em đang có sẵn thông tin giao hàng rồi ạ. Nếu mình chốt {productName} thì chị giúp em xác nhận lại SĐT {phone} và địa chỉ {address} còn dùng đúng không ạ?";
            }

            return string.IsNullOrWhiteSpace(productName)
                ? "Dạ em đang có đủ thông tin giao hàng của chị rồi ạ. Khi chị chốt sản phẩm em lên đơn ngay cho mình nha."
                : $"Dạ em đang có đủ thông tin để chốt {productName} cho chị rồi ạ. Nếu chị đồng ý em lên đơn theo thông tin này cho mình nha.";
        }

        var missing = FormatMissingFields(_promptBuilder.GetMissingContactInfo(ctx));
        return string.IsNullOrWhiteSpace(productName)
            ? $"Dạ em chưa đủ thông tin của chị ạ. Chị gửi em {missing} giúp em nha."
            : $"Dạ để em chốt đúng {productName} cho chị thì chị gửi em {missing} giúp em nha.";
    }

    /// <inheritdoc/>
    public async Task<string?> BuildContactCollectionReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        var productName = product?.Name;
        var missingInfo = _promptBuilder.GetMissingContactInfo(ctx);
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;

        // Have remembered contact — ask customer to confirm or replace
        if (needsConfirmation && missingInfo.Count == 0)
        {
            var phone = ctx.GetData<string>("customerPhone");
            var address = ctx.GetData<string>("shippingAddress");
            return string.IsNullOrWhiteSpace(productName)
                ? $"Dạ em đang có thông tin giao hàng lần trước của chị rồi ạ. Chị giúp em xác nhận SĐT {phone} và địa chỉ {address} còn dùng đúng không, hay chị muốn đổi thông tin mới để em lên đơn cho lần này ạ?"
                : $"Dạ em đang có sẵn thông tin để chốt {productName} cho chị rồi ạ. Chị giúp em xác nhận SĐT {phone} và địa chỉ {address} còn dùng đúng không, hay chị muốn đổi thông tin mới để em lên đơn cho lần này ạ?";
        }

        // All info present and confirmed — no reply needed
        if (missingInfo.Count == 0) return null;

        // Missing one or both fields — ask for them
        var missing = FormatMissingFields(missingInfo);
        return string.IsNullOrWhiteSpace(productName)
            ? $"Dạ chị gửi em {missing} để em lên đơn cho mình nha."
            : $"Dạ chị gửi em {missing} để em chốt {productName} cho mình nha.";
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if message is a generic buy signal (ok, chốt, lên đơn, etc.).
    /// Does NOT check contact state — caller must check IsAwaitingOldContactConfirmation separately.
    ///
    /// Rejection guard fires first: phrases meaning "confirmed / using old info" (đúng rồi, vẫn dùng,
    /// như cũ) are NOT buy signals — they confirm the remembered contact.
    /// </summary>
    private static bool IsGenericBuyContinuation(string message)
    {
        var normalized = SalesTextHelper.NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        // Rejection guard: these phrases confirm remembered contact, not a generic buy
        if (normalized.Contains("dung roi", StringComparison.Ordinal)
            || normalized.Contains("van dung", StringComparison.Ordinal)
            || normalized.Contains("nhu cu", StringComparison.Ordinal)
            || normalized.Contains("thong tin cu", StringComparison.Ordinal)
            || normalized.Contains("cu nhu vay", StringComparison.Ordinal))
            return false;

        // Exact-match buy signals
        if (normalized is "ok" or "oke" or "okay" or "ok e" or "ok em" or "oke e" or "oke em")
            return true;

        // Substring buy signals
        return normalized.Contains("len don", StringComparison.Ordinal)
               || normalized.Contains("chot", StringComparison.Ordinal)
               || normalized.Contains("dat hang", StringComparison.Ordinal)
               || normalized.Contains("dat luon", StringComparison.Ordinal)
               || normalized.Contains("mua luon", StringComparison.Ordinal)
               || normalized.Contains("lay san pham nay", StringComparison.Ordinal)
               || normalized.Contains("lay nha", StringComparison.Ordinal)
               || normalized.Contains("lay nhe", StringComparison.Ordinal);
    }

    /// <summary>Converts internal field names (e.g. "so dien thoai") to display Vietnamese.</summary>
    private static string FormatMissingFields(List<string> fields)
        => string.Join(" và ", fields.Select(f => f == "so dien thoai" ? "số điện thoại" : "địa chỉ"));

    private static bool ContainsAnyPhrase(string message, params string[] phrases)
        => phrases.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));
}
