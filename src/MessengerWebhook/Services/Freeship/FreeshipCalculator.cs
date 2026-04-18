namespace MessengerWebhook.Services.Freeship;

/// <summary>
/// Calculator for freeship eligibility and shipping fees.
/// </summary>
public class FreeshipCalculator : IFreeshipCalculator
{
    private const decimal ShippingFee = 30000m;

    public bool IsEligibleForFreeship(List<string> productCodes)
    {
        if (productCodes == null || productCodes.Count == 0)
        {
            return false;
        }

        return false;
    }

    public decimal CalculateShippingFee(List<string> productCodes)
    {
        return IsEligibleForFreeship(productCodes) ? 0 : ShippingFee;
    }

    public string GetFreeshipMessage(bool isEligible)
    {
        return isEligible
            ? "Don nay dang duoc mien phi van chuyen theo chinh sach hien tai."
            : $"Hien tai don nay chua nam trong chinh sach freeship, phi ship tam tinh la {ShippingFee:N0}d.";
    }
}
