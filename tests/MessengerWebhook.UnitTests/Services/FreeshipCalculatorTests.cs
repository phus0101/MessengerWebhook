using MessengerWebhook.Services.Freeship;

namespace MessengerWebhook.UnitTests.Services;

public class FreeshipCalculatorTests
{
    private readonly FreeshipCalculator _calculator = new();

    [Fact]
    public void IsEligibleForFreeship_TwoProducts_ReturnsTrue()
    {
        Assert.True(_calculator.IsEligibleForFreeship(new List<string> { "KCN", "KL" }));
    }

    [Fact]
    public void IsEligibleForFreeship_SingleProduct_ReturnsFalse()
    {
        Assert.False(_calculator.IsEligibleForFreeship(new List<string> { "KCN" }));
    }

    [Fact]
    public void IsEligibleForFreeship_ComboProduct_ReturnsTrue()
    {
        Assert.True(_calculator.IsEligibleForFreeship(new List<string> { "COMBO_2" }));
    }

    [Fact]
    public void CalculateShippingFee_NotEligible_Returns30000()
    {
        Assert.Equal(30000m, _calculator.CalculateShippingFee(new List<string> { "KCN" }));
    }

    [Fact]
    public void GetFreeshipMessage_UsesCurrentSalesCopy()
    {
        Assert.Equal("Mien phi van chuyen", _calculator.GetFreeshipMessage(true));
        Assert.Contains("30,000d", _calculator.GetFreeshipMessage(false));
    }
}
