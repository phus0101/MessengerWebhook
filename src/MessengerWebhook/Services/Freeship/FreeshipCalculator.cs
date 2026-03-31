namespace MessengerWebhook.Services.Freeship;

/// <summary>
/// Calculator for freeship eligibility and shipping fees.
/// </summary>
public class FreeshipCalculator : IFreeshipCalculator
{
    private const decimal ShippingFee = 30000m;
    private const string ComboProductCode = "COMBO_2";

    public bool IsEligibleForFreeship(List<string> productCodes)
    {
        if (productCodes == null || productCodes.Count == 0)
        {
            return false;
        }

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
            ? "Mien phi van chuyen"
            : $"Phi van chuyen tam tinh: {ShippingFee:N0}d";
    }
}
