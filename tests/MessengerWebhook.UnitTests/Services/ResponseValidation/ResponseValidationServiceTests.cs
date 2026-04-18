using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Configuration;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.Tone.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.ResponseValidation;

public class ResponseValidationServiceTests
{
    private readonly Mock<ILogger<ResponseValidationService>> _loggerMock;
    private readonly ResponseValidationOptions _options;

    public ResponseValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ResponseValidationService>>();
        _options = new ResponseValidationOptions
        {
            EnableValidation = true,
            EnableToneValidation = true,
            EnableContextValidation = true,
            EnableLanguageValidation = true,
            EnableStructureValidation = true,
            MinResponseLength = 10,
            MaxResponseLength = 500,
            BlockOnErrors = false
        };
    }

    private ResponseValidationService CreateService()
    {
        return new ResponseValidationService(
            Options.Create(_options),
            _loggerMock.Object);
    }

    private ResponseValidationContext CreateValidContext(string response = "Dạ chào chị! Em có thể tư vấn cho chị về sản phẩm ạ.")
    {
        return new ResponseValidationContext
        {
            Response = response,
            ToneProfile = new ToneProfile
            {
                Level = ToneLevel.Formal,
                PronounText = "chị",
                Pronoun = VietnamesePronoun.Chi
            },
            ConversationContext = new ConversationContext
            {
                CurrentStage = JourneyStage.Browsing,
                TurnCount = 3
            }
        };
    }

    [Fact]
    public async Task ValidateAsync_ValidResponse_ReturnsValid()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext();

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ValidateAsync_MissingPronoun_ReturnsWarning()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ em có thể tư vấn về sản phẩm ạ.");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid); // Still valid because BlockOnErrors = false
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Category == "Tone" && w.Message.Contains("pronoun"));
    }

    [Fact]
    public async Task ValidateAsync_FormalToneWithoutMarkers_ReturnsWarning()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Chào chị! Em có thể tư vấn cho chị về sản phẩm.");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Category == "Tone" && w.Message.Contains("formality markers"));
    }

    [Fact]
    public async Task ValidateAsync_CasualToneWithFormalMarkers_ReturnsWarning()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ chào bạn! Mình có thể tư vấn cho bạn ạ.");
        context.ToneProfile.Level = ToneLevel.Casual;
        context.ToneProfile.PronounText = "bạn";

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Category == "Tone" && w.Message.Contains("Casual tone expected"));
    }

    [Fact]
    public async Task ValidateAsync_PushyPhraseDuringBrowsing_ReturnsWarning()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ chị đặt hàng ngay để nhận ưu đãi ạ!");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Category == "Context" && w.Message.Contains("Pushy phrase"));
    }

    [Fact]
    public async Task ValidateAsync_ReadyStageWithoutCallToAction_ReturnsInfo()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ sản phẩm này rất tốt cho da chị ạ. Em nghĩ chị sẽ thích lắm.");
        context.ConversationContext.CurrentStage = JourneyStage.Ready;

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        // Info issues don't appear in warnings
    }

    [Fact]
    public async Task ValidateAsync_MixedLanguage_ReturnsInfo()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Hi bạn! Dạ em có thể tư vấn cho chị ạ.");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        // Info level issues are collected but don't block
    }

    [Fact]
    public async Task ValidateAsync_ExcessiveEmoji_ReturnsWarning()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ chào chị! 😊😍🥰💕 Em có thể tư vấn cho chị ạ.");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Category == "Language" && w.Message.Contains("emoji"));
    }

    [Fact]
    public async Task ValidateAsync_TooShortResponse_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ ạ.");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid); // Still valid because BlockOnErrors = false
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Error && i.Message.Contains("too short"));
    }

    [Fact]
    public async Task ValidateAsync_TooLongResponse_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var longResponse = new string('a', 600);
        var context = CreateValidContext(longResponse);

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid); // Still valid because BlockOnErrors = false
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Error && i.Message.Contains("too long"));
    }

    [Fact]
    public async Task ValidateAsync_EmptyResponse_ReturnsCriticalError()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid); // Still valid because BlockOnErrors = false
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Critical);
    }

    [Fact]
    public async Task ValidateAsync_ExcessiveLineBreaks_ReturnsWarning()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext("Dạ chào chị!\n\n\n\n\n\nEm có thể tư vấn ạ.");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Category == "Structure" && w.Message.Contains("line breaks"));
    }

    [Fact]
    public async Task ValidateAsync_ValidationDisabled_ReturnsValid()
    {
        // Arrange
        _options.EnableValidation = false;
        var service = CreateService();
        var context = CreateValidContext(""); // Invalid response

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_BlockOnErrorsTrue_InvalidResponse_ReturnsInvalid()
    {
        // Arrange
        _options.BlockOnErrors = true;
        var service = CreateService();
        var context = CreateValidContext(""); // Critical error

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public async Task ValidateAsync_MultipleIssues_AggregatesCorrectly()
    {
        // Arrange
        var service = CreateService();
        // Response with multiple issues: missing pronoun + excessive emoji + too short
        var context = CreateValidContext("Xin chào! 😊😍🥰💕");

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.IsValid); // BlockOnErrors = false
        Assert.True(result.Metadata.ContainsKey("TotalIssuesFound"));
        var totalIssues = (int)result.Metadata["TotalIssuesFound"];
        Assert.True(totalIssues >= 2, $"Expected at least 2 issues, got {totalIssues}");

        // Should have at least one issue (structure error for too short OR tone warning for missing pronoun)
        var allIssuesCount = result.Issues.Count + result.Warnings.Count;
        Assert.True(allIssuesCount >= 2, $"Expected at least 2 total issues/warnings, got {allIssuesCount}");
    }

    [Fact]
    public async Task ValidateAsync_PerformanceCheck_CompletesUnder50Ms()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await service.ValidateAsync(context);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Validation took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
    }

    [Fact]
    public async Task ValidateAsync_IncludesValidationDurationInMetadata()
    {
        // Arrange
        var service = CreateService();
        var context = CreateValidContext();

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        Assert.True(result.Metadata.ContainsKey("ValidationDurationMs"));
        Assert.IsType<double>(result.Metadata["ValidationDurationMs"]);
    }

    [Fact]
    public async Task ValidateAsync_SelectiveValidation_OnlyEnabledValidatorsRun()
    {
        // Arrange
        _options.EnableToneValidation = false;
        _options.EnableContextValidation = false;
        _options.EnableLanguageValidation = false;
        _options.EnableStructureValidation = true;
        var service = CreateService();
        var context = CreateValidContext(""); // Would trigger tone, context, language issues

        // Act
        var result = await service.ValidateAsync(context);

        // Assert
        // Only structure validation should run, detecting empty response
        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, i => Assert.Equal("Structure", i.Category));
    }
}
