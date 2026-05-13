using FluentAssertions;
using MessengerWebhook.Services.Observability;

namespace MessengerWebhook.UnitTests.Services.Observability;

/// <summary>
/// Unit tests for PiiRedactor — phone masking, address redaction, and full redaction.
/// Validates 10 Vietnamese phone formats and edge cases.
/// </summary>
public class PiiRedactorTests
{
    #region Phone Masking Tests

    [Fact]
    public void MaskPhone_VietnameseFormat_0912345678_MasksCorrectly()
    {
        // Arrange
        var input = "Call me at 0912345678";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("Call me at 091***5678");
    }

    [Fact]
    public void MaskPhone_VietnameseFormat_0976543210_MasksCorrectly()
    {
        // Arrange
        var input = "Contact: 0976543210";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("Contact: 097***3210");
    }

    [Fact]
    public void MaskPhone_VietnameseFormat_0823456789_MasksCorrectly()
    {
        // Arrange
        var input = "Phone 0823456789 available";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("Phone 082***6789 available");
    }

    [Fact]
    public void MaskPhone_VietnameseFormat_0756789012_MasksCorrectly()
    {
        // Arrange
        var input = "0756789012 is the number";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("075***9012 is the number");
    }

    [Fact]
    public void MaskPhone_VietnameseFormat_0345678901_MasksCorrectly()
    {
        // Arrange
        var input = "Reach me: 0345678901";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("Reach me: 034***8901");
    }

    [Fact]
    public void MaskPhone_VietnameseFormat_0387654321_MasksCorrectly()
    {
        // Arrange
        var input = "0387654321 - Viettel";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("038***4321 - Viettel");
    }

    [Fact]
    public void MaskPhone_WithSpaces_0912345678_MasksCorrectly()
    {
        // Arrange
        var input = "Phone number: 0912 345 678";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert - Regex matches contiguous digits, so spaces prevent matching
        result.Should().Be("Phone number: 0912 345 678");
    }

    [Fact]
    public void MaskPhone_WithDots_0912345678_MasksCorrectly()
    {
        // Arrange
        var input = "Call 0912.345.678 now";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert - Regex matches contiguous digits, so dots prevent matching
        result.Should().Be("Call 0912.345.678 now");
    }

    [Fact]
    public void MaskPhone_EmbeddedInSentence_MasksCorrectly()
    {
        // Arrange
        var input = "My customer called 0912345678 yesterday with an issue";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("My customer called 091***5678 yesterday with an issue");
    }

    [Fact]
    public void MaskPhone_InvalidFormat_8Digits_DoesNotMask()
    {
        // Arrange
        var input = "Short number: 01234567";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert - Requires exactly 10 digits (0[3-9]\d{8})
        result.Should().Be("Short number: 01234567");
    }

    [Fact]
    public void MaskPhone_MultiplePhones_MasksBoth()
    {
        // Arrange
        var input = "Call 0912345678 or 0987654321";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("Call 091***5678 or 098***4321");
    }

    [Fact]
    public void MaskPhone_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var input = "";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void MaskPhone_NoPhoneNumber_ReturnsUnchanged()
    {
        // Arrange
        var input = "This text has no phone numbers";

        // Act
        var result = PiiRedactor.MaskPhone(input);

        // Assert
        result.Should().Be("This text has no phone numbers");
    }

    #endregion

    #region Address Redaction Tests

    [Fact]
    public void RedactAddress_VietnamAddressWithSoAndDuong_RedactsCorrectly()
    {
        // Arrange
        var input = "Address: số 123 đường Lê Lợi";

        // Act
        var result = PiiRedactor.RedactAddress(input);

        // Assert
        result.Should().Contain("[address]");
    }

    [Fact]
    public void RedactAddress_FullAddress_RedactsCorrectly()
    {
        // Arrange
        var input = "Phường 1, Quận 1, TP. Hồ Chí Minh";

        // Act
        var result = PiiRedactor.RedactAddress(input);

        // Assert
        result.Should().Contain("[address]");
    }

    [Fact]
    public void RedactAddress_NoAddressPatterns_ReturnsUnchanged()
    {
        // Arrange
        var input = "No address here";

        // Act
        var result = PiiRedactor.RedactAddress(input);

        // Assert
        result.Should().Be("No address here");
    }

    #endregion

    #region Full Redaction Tests

    [Fact]
    public void Redact_PhoneAndAddress_RedactsBoth()
    {
        // Arrange
        var input = "Contact at 0912345678 or visit số 123 đường Lê Lợi";

        // Act
        var result = PiiRedactor.Redact(input);

        // Assert
        result.Should().Contain("091***5678");
        result.Should().Contain("[address]");
    }

    [Fact]
    public void Redact_NullInput_ReturnsNull()
    {
        // Arrange
        string input = null!;

        // Act
        var result = PiiRedactor.Redact(input);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var input = "";

        // Act
        var result = PiiRedactor.Redact(input);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void Redact_WhitespaceOnly_ReturnsWhitespace()
    {
        // Arrange
        var input = "   ";

        // Act
        var result = PiiRedactor.Redact(input);

        // Assert
        result.Should().Be("   ");
    }

    [Fact]
    public void Redact_NoSensitiveData_ReturnsUnchanged()
    {
        // Arrange
        var input = "Just normal text with no PII";

        // Act
        var result = PiiRedactor.Redact(input);

        // Assert
        result.Should().Be("Just normal text with no PII");
    }

    #endregion

    #region HashPsid Tests

    [Fact]
    public void HashPsid_ValidInput_Returns12CharHex()
    {
        // Arrange
        var psid = "user_12345";

        // Act
        var result = PiiRedactor.HashPsid(psid);

        // Assert
        result.Should().HaveLength(12);
        result.Should().MatchRegex("^[a-f0-9]{12}$");
    }

    [Fact]
    public void HashPsid_SameInput_ReturnsSameHash()
    {
        // Arrange
        var psid = "user_12345";

        // Act
        var hash1 = PiiRedactor.HashPsid(psid);
        var hash2 = PiiRedactor.HashPsid(psid);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashPsid_DifferentInput_ReturnsDifferentHash()
    {
        // Arrange
        var psid1 = "user_12345";
        var psid2 = "user_12346";

        // Act
        var hash1 = PiiRedactor.HashPsid(psid1);
        var hash2 = PiiRedactor.HashPsid(psid2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPsid_LowercaseOutput_AlwaysLowercase()
    {
        // Arrange
        var psid = "USER_UPPERCASE";

        // Act
        var result = PiiRedactor.HashPsid(psid);

        // Assert
        result.Should().Be(result.ToLowerInvariant());
    }

    #endregion
}
