using System.Text.RegularExpressions;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.StateMachine.Handlers;

internal static partial class SalesMessageParser
{
    private static readonly string[] AddressHints =
    {
        "dia chi", "ship", "giao ve", "gui ve", "phuong", "xa", "quan",
        "huyen", "tinh", "thanh pho", "so nha", "duong"
    };
    private static readonly string[] RememberedContactConfirmationHints =
    {
        "nhu cu", "thong tin cu", "dia chi cu", "so cu", "dung roi", "đúng rồi", "van dung",
        "ok em", "oke em", "len don", "chot don", "gui don", "cu nhu vay"
    };
    private static readonly string[] QuestionMarkers =
    {
        "?", "bao lau", "bao nhieu", "khong", "the nao", "nhu nao", "khi nao"
    };

    public static async Task CaptureCustomerDetailsAsync(
        StateContext context,
        string message,
        IGeminiService geminiService,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        // Fast path: Try explicit extraction first
        var capturedAnyField = false;
        var phone = TryExtractPhone(message);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            context.SetData("customerPhone", phone);
            capturedAnyField = true;
            logger?.LogInformation("Captured phone from message for PSID {PSID}", context.FacebookPSID);
        }

        var address = TryExtractAddress(message);
        if (!string.IsNullOrWhiteSpace(address))
        {
            context.SetData("shippingAddress", address);
            capturedAnyField = true;
            logger?.LogInformation("Captured address from message for PSID {PSID}", context.FacebookPSID);
        }

        if (capturedAnyField)
        {
            context.SetData("contactNeedsConfirmation", false);
            context.SetData("contactMemorySource", "current-chat");
            logger?.LogInformation(
                "Confirmation detection: Method=explicit-data, Message='{Message}', PSID={PSID}",
                message, context.FacebookPSID);
            return;
        }

        // AI path: Check for confirmation if needed
        if (NeedsContactConfirmation(context))
        {
            var isConfirming = await IsConfirmingRememberedContactAsync(
                message,
                context,
                geminiService,
                logger,
                cancellationToken);

            if (isConfirming)
            {
                context.SetData("contactNeedsConfirmation", false);
                logger?.LogInformation(
                    "Customer confirmed remembered contact. Message: '{Message}' for PSID {PSID}",
                    message, context.FacebookPSID);
            }
        }
    }

    public static bool HasRequiredContact(StateContext context)
    {
        return !string.IsNullOrWhiteSpace(context.GetData<string>("customerPhone")) &&
               !string.IsNullOrWhiteSpace(context.GetData<string>("shippingAddress")) &&
               !NeedsContactConfirmation(context);
    }

    public static string BuildMissingInfoPrompt(StateContext context)
    {
        var rememberedPhone = context.GetData<string>("rememberedCustomerPhone");
        var rememberedAddress = context.GetData<string>("rememberedShippingAddress");
        var needsConfirmation = context.GetData<bool?>("contactNeedsConfirmation") == true;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(context.GetData<string>("customerPhone")))
        {
            missing.Add("so dien thoai");
        }

        if (string.IsNullOrWhiteSpace(context.GetData<string>("shippingAddress")))
        {
            missing.Add("dia chi");
        }

        return missing.Count switch
        {
            0 when needsConfirmation => "Em dang giu so dien thoai va dia chi lan truoc cua chi roi, neu minh van dung thong tin cu em len don luon nha. Neu chi doi so hoac doi dia chi thi nhan em cap nhat lien.",
            0 => "Chi xac nhan thong tin giup em de em chuyen don nha.",
            1 when missing[0] == "so dien thoai" && !string.IsNullOrWhiteSpace(rememberedAddress)
                => "Em dang giu dia chi giao hang lan truoc roi, chi gui em xin so dien thoai de em len don nha. Neu dia chi co doi thi chi nhan em cap nhat luon giup em.",
            1 when missing[0] == "dia chi" && !string.IsNullOrWhiteSpace(rememberedPhone)
                => "Em dang giu so dien thoai lan truoc roi, chi gui em xin dia chi giao hang de em len don nha. Neu so dien thoai co doi thi chi nhan em cap nhat luon giup em.",
            1 => $"Chi iu cho em xin {missing[0]} de em len don luon nha.",
            _ => "Chi iu cho em xin so dien thoai va dia chi em len don luon nha."
        };
    }

    private static string? TryExtractPhone(string message)
    {
        var match = PhoneRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        var digits = new string(match.Value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal))
        {
            digits = $"0{digits[2..]}";
        }

        return digits.Length is >= 9 and <= 11 ? digits : null;
    }

    private static string? TryExtractAddress(string message)
    {
        var cleaned = PhoneRegex().Replace(message, " ");
        var normalized = cleaned.Trim().Trim(',', '.', ';', ':', '-');
        if (normalized.Length < 10)
        {
            return null;
        }

        // Reject if message is a question
        if (normalized.Contains('?'))
        {
            return null;
        }

        if (AddressHints.Any(hint => normalized.Contains(hint, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        if (normalized.Contains(',') || normalized.Contains('/'))
        {
            return normalized;
        }

        return null;
    }

    private static bool NeedsContactConfirmation(StateContext context)
    {
        return context.GetData<bool?>("contactNeedsConfirmation") == true;
    }

    private static async Task<bool> IsConfirmingRememberedContactAsync(
        string message,
        StateContext context,
        IGeminiService geminiService,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var normalized = message.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        // Check if ambiguous (has keywords but also question markers)
        var hasKeywords = RememberedContactConfirmationHints.Any(normalized.Contains);
        if (!hasKeywords)
        {
            return false;
        }

        var hasQuestionMarkers = QuestionMarkers.Any(normalized.Contains);
        if (!hasQuestionMarkers)
        {
            // Clear confirmation keywords without question markers
            logger?.LogInformation(
                "Confirmation detection: Method=keyword-match, Confidence=1.0, Message='{Message}', PSID={PSID}",
                message, context.FacebookPSID);
            return true;
        }

        // Ambiguous case - use AI reasoning
        var phone = context.GetData<string>("customerPhone") ?? string.Empty;
        var address = context.GetData<string>("shippingAddress") ?? string.Empty;

        var result = await geminiService.DetectConfirmationAsync(
            message,
            phone,
            address,
            cancellationToken);

        logger?.LogInformation(
            "Confirmation detection: Method={Method}, IsConfirming={IsConfirming}, Confidence={Confidence}, Reason='{Reason}', Message='{Message}', PSID={PSID}",
            result.DetectionMethod, result.IsConfirming, result.Confidence, result.Reason, message, context.FacebookPSID);

        return result.IsConfirming && result.Confidence >= 0.7;
    }

    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)(?:[\s\.-]?\d){8,10}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();
}
