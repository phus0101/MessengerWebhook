using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services;

/// <summary>
/// Unit tests for SignatureValidator HMAC-SHA256 validation
/// </summary>
public class SignatureValidatorTests
{
    private const string TestAppSecret = "test_app_secret_12345";
    private readonly Mock<ILogger<SignatureValidator>> _loggerMock;
    private readonly IOptions<FacebookOptions> _options;

    public SignatureValidatorTests()
    {
        _loggerMock = new Mock<ILogger<SignatureValidator>>();
        _options = Options.Create(new FacebookOptions { AppSecret = TestAppSecret });
    }

    private SignatureValidator CreateValidator()
    {
        return new SignatureValidator(_options, _loggerMock.Object);
    }

    private string ComputeValidSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestAppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLower();
    }

    [Fact]
    public async Task ValidSignature_ReturnsTrue()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[]}";
        var validSignature = ComputeValidSignature(rawBody);

        // Act
        var result = await validator.ValidateAsync(rawBody, validSignature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidSignature_ReturnsFalse()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[]}";
        var invalidSignature = "sha256=0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = await validator.ValidateAsync(rawBody, invalidSignature);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MissingSignature_ReturnsFalse()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[]}";

        // Act
        var result = await validator.ValidateAsync(rawBody, string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NullSignature_ReturnsFalse()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[]}";

        // Act
        var result = await validator.ValidateAsync(rawBody, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WrongFormat_ReturnsFalse()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[]}";
        var signatureWithoutPrefix = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Act
        var result = await validator.ValidateAsync(rawBody, signatureWithoutPrefix);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WrongPrefix_ReturnsFalse()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[]}";
        var signatureWithWrongPrefix = "sha1=abcdef1234567890abcdef1234567890abcdef12";

        // Act
        var result = await validator.ValidateAsync(rawBody, signatureWithWrongPrefix);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidSignature_DifferentBody_ReturnsTrue()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\",\"entry\":[{\"id\":\"123\",\"time\":1234567890}]}";
        var validSignature = ComputeValidSignature(rawBody);

        // Act
        var result = await validator.ValidateAsync(rawBody, validSignature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidSignature_EmptyBody_ReturnsFalse()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "";
        var validSignature = ComputeValidSignature(rawBody);

        // Act
        var result = await validator.ValidateAsync(rawBody, validSignature);

        // Assert
        result.Should().BeFalse(); // Security fix: empty body is rejected
    }

    [Fact]
    public async Task ValidSignature_UppercaseHash_ReturnsTrue()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestAppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var uppercaseSignature = "sha256=" + Convert.ToHexString(hash).ToUpper();

        // Act
        var result = await validator.ValidateAsync(rawBody, uppercaseSignature);

        // Assert
        result.Should().BeTrue(); // Security fix: hash normalized to lowercase before comparison
    }

    [Fact]
    public async Task ValidSignature_CaseInsensitivePrefix_ReturnsTrue()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"object\":\"page\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestAppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var signatureWithUpperPrefix = "SHA256=" + Convert.ToHexString(hash).ToLower();

        // Act
        var result = await validator.ValidateAsync(rawBody, signatureWithUpperPrefix);

        // Assert
        result.Should().BeTrue(); // Prefix check is case-insensitive
    }

    [Fact]
    public async Task ModifiedBody_InvalidatesSignature()
    {
        // Arrange
        var validator = CreateValidator();
        var originalBody = "{\"object\":\"page\",\"entry\":[]}";
        var modifiedBody = "{\"object\":\"page\",\"entry\":[],\"extra\":\"field\"}";
        var signatureForOriginal = ComputeValidSignature(originalBody);

        // Act
        var result = await validator.ValidateAsync(modifiedBody, signatureForOriginal);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidSignature_LargePayload_ReturnsTrue()
    {
        // Arrange
        var validator = CreateValidator();
        var largeBody = new string('x', 10000);
        var validSignature = ComputeValidSignature(largeBody);

        // Act
        var result = await validator.ValidateAsync(largeBody, validSignature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidSignature_SpecialCharacters_ReturnsTrue()
    {
        // Arrange
        var validator = CreateValidator();
        var rawBody = "{\"message\":\"Hello 世界! 🌍 Special: <>&\\\"'\"}";
        var validSignature = ComputeValidSignature(rawBody);

        // Act
        var result = await validator.ValidateAsync(rawBody, validSignature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullAppSecret_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsWithNullSecret = Options.Create(new FacebookOptions { AppSecret = null! });

        // Act & Assert
        var act = () => new SignatureValidator(optionsWithNullSecret, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }
}
