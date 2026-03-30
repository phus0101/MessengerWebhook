using MessengerWebhook.Services.Freeship;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class FreeshipCalculatorTests
{
    private readonly FreeshipCalculator _calculator;

    public FreeshipCalculatorTests()
    {
        _calculator = new FreeshipCalculator();
    }

    [Fact]
    public void IsEligibleForFreeship_TwoProducts_ReturnsTrue()
    {
        // Arrange
        var productCodes = new List<string> { "KCN", "KL" };

        // Act
        var result = _calculator.IsEligibleForFreeship(productCodes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEligibleForFreeship_SingleProduct_ReturnsFalse()
    {
        // Arrange
        var productCodes = new List<string> { "KCN" };

        // Act
        var result = _calculator.IsEligibleForFreeship(productCodes);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForFreeship_ComboProduct_ReturnsTrue()
    {
        // Arrange
        var productCodes = new List<string> { "COMBO_2" };

        // Act
        var result = _calculator.IsEligibleForFreeship(productCodes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEligibleForFreeship_EmptyList_ReturnsFalse()
    {
        // Arrange
        var productCodes = new List<string>();

        // Act
        var result = _calculator.IsEligibleForFreeship(productCodes);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CalculateShippingFee_Eligible_ReturnsZero()
    {
        // Arrange
        var productCodes = new List<string> { "KCN", "KL" };

        // Act
        var result = _calculator.CalculateShippingFee(productCodes);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateShippingFee_NotEligible_Returns30000()
    {
        // Arrange
        var productCodes = new List<string> { "KCN" };

        // Act
        var result = _calculator.CalculateShippingFee(productCodes);

        // Assert
        Assert.Equal(30000m, result);
    }

    [Fact]
    public void GetFreeshipMessage_Eligible_ReturnsFreeship()
    {
        // Act
        var result = _calculator.GetFreeshipMessage(true);

        // Assert
        Assert.Equal("(Miễn phí vận chuyển)", result);
    }

    [Fact]
    public void GetFreeshipMessage_NotEligible_ReturnsShippingFee()
    {
        // Act
        var result = _calculator.GetFreeshipMessage(false);

        // Assert
        Assert.Contains("30.000đ", result);
    }
}
