using MessengerWebhook.Services.Freeship;

namespace MessengerWebhook.UnitTests.Services;

public class FreeshipCalculatorTests
{
    private readonly FreeshipCalculator _calculator = new();

    [Fact]
    public void IsEligibleForFreeship_TwoProductsWithoutCombo_ReturnsFalse()
    {
        Assert.False(_calculator.IsEligibleForFreeship(new List<string> { "KCN", "KL" }));
    }

    [Fact]
    public void IsEligibleForFreeship_SingleProduct_ReturnsFalse()
    {
        Assert.False(_calculator.IsEligibleForFreeship(new List<string> { "KCN" }));
    }

    [Fact]
    public void IsEligibleForFreeship_ComboProductWithoutPolicy_ReturnsFalse()
    {
        Assert.False(_calculator.IsEligibleForFreeship(new List<string> { "COMBO_2" }));
    }

    [Fact]
    public void CalculateShippingFee_NotEligible_Returns30000()
    {
        Assert.Equal(30000m, _calculator.CalculateShippingFee(new List<string> { "KCN" }));
    }

    [Fact]
    public void GetFreeshipMessage_UsesPolicyDrivenCopy()
    {
        Assert.Contains("chinh sach hien tai", _calculator.GetFreeshipMessage(true));
        Assert.Contains("chua nam trong chinh sach freeship", _calculator.GetFreeshipMessage(false));
        Assert.Contains("30,000d", _calculator.GetFreeshipMessage(false));
    }
}
