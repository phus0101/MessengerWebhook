namespace MessengerWebhook.Services.Freeship;

/// <summary>
/// Calculator for freeship eligibility and shipping fees
/// </summary>
public class FreeshipCalculator : IFreeshipCalculator
{
    private const decimal ShippingFee = 30000m;
    private const string ComboProductCode = "COMBO_2";

    public bool IsEligibleForFreeship(List<string> productCodes)
    {
        if (productCodes == null || productCodes.Count == 0)
            return false;

        // Eligible if: >= 2 products OR contains COMBO_2
        return productCodes.Count >= 2 ||
               productCodes.Any(code => code.Equals(ComboProductCode, StringComparison.OrdinalIgnoreCase));
    }

    public decimal CalculateShippingFee(List<string> productCodes)
    {
        return IsEligibleForFreeship(productCodes) ? 0 : ShippingFee;
    }

    public string GetFreeshipMessage(bool isEligible)
    {
        return isEligible
            ? "(Miễn phí vận chuyển)"
            : $"(Phí vận chuyển: {ShippingFee:N0}đ)";
    }
}
