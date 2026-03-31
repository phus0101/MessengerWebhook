using System.Text.RegularExpressions;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.StateMachine.Handlers;

internal static partial class SalesMessageParser
{
    private static readonly string[] AddressHints =
    {
        "dia chi", "ship", "giao ve", "gui ve", "phuong", "xa", "quan",
        "huyen", "tinh", "thanh pho", "so nha", "duong"
    };

    public static void CaptureCustomerDetails(StateContext context, string message)
    {
        var phone = TryExtractPhone(message);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            context.SetData("customerPhone", phone);
        }

        var address = TryExtractAddress(message);
        if (!string.IsNullOrWhiteSpace(address))
        {
            context.SetData("shippingAddress", address);
        }
    }

    public static bool HasRequiredContact(StateContext context)
    {
        return !string.IsNullOrWhiteSpace(context.GetData<string>("customerPhone")) &&
               !string.IsNullOrWhiteSpace(context.GetData<string>("shippingAddress"));
    }

    public static string BuildMissingInfoPrompt(StateContext context)
    {
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
            0 => "Chi xac nhan thong tin giup em de em chuyen don nha.",
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

        if (AddressHints.Any(hint => normalized.Contains(hint, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        if (normalized.Contains(',') || normalized.Contains('/') || normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4)
        {
            return normalized;
        }

        return null;
    }

    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)(?:[\s\.-]?\d){8,10}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();
}
