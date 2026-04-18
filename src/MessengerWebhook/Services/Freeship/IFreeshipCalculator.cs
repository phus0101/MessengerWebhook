namespace MessengerWebhook.Services.Freeship;

/// <summary>
/// Calculator for freeship eligibility and shipping fees
/// </summary>
public interface IFreeshipCalculator
{
    /// <summary>
    /// Check if order is eligible for freeship based on current policy.
    /// </summary>
    bool IsEligibleForFreeship(List<string> productCodes);

    /// <summary>
    /// Calculate shipping fee
    /// Returns 0 if eligible for freeship, otherwise 30,000 VND
    /// </summary>
    decimal CalculateShippingFee(List<string> productCodes);

    /// <summary>
    /// Get freeship message for response
    /// </summary>
    string GetFreeshipMessage(bool isEligible);
}
