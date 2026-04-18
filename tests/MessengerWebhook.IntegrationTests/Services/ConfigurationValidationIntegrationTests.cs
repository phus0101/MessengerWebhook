using MessengerWebhook.Services.Emotion.Configuration;
using MessengerWebhook.Services.Tone.Configuration;
using MessengerWebhook.Services.Conversation.Configuration;
using MessengerWebhook.Services.SmallTalk.Configuration;
using MessengerWebhook.Services.ResponseValidation.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services;

/// <summary>
/// Configuration validation integration tests for naturalness services.
/// Verifies that all Options validators work correctly and invalid configs are rejected.
/// </summary>
public class ConfigurationValidationIntegrationTests
{
    [Fact]
    public void Configuration_EmotionOptions_ValidConfig_ShouldPass()
    {
        // Arrange
        var validOptions = new EmotionDetectionOptions
        {
            EnableContextAnalysis = true,
            ContextWindowSize = 3,
            ConfidenceThreshold = 0.6,
            EnableCaching = true,
            CacheDurationMinutes = 5
        };

        var validator = new ValidateEmotionDetectionOptions();

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Configuration_EmotionOptions_InvalidThreshold_ShouldFail()
    {
        // Arrange
        var invalidOptions = new EmotionDetectionOptions
        {
            ConfidenceThreshold = 1.5 // > 1.0
        };

        var validator = new ValidateEmotionDetectionOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("ConfidenceThreshold", result.FailureMessage);
    }

    [Fact]
    public void Configuration_EmotionOptions_NegativeThreshold_ShouldFail()
    {
        // Arrange
        var invalidOptions = new EmotionDetectionOptions
        {
            ConfidenceThreshold = -0.1
        };

        var validator = new ValidateEmotionDetectionOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("ConfidenceThreshold", result.FailureMessage);
    }

    [Fact]
    public void Configuration_EmotionOptions_InvalidContextWindow_ShouldFail()
    {
        // Arrange
        var invalidOptions = new EmotionDetectionOptions
        {
            ContextWindowSize = 0 // < 1
        };

        var validator = new ValidateEmotionDetectionOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("ContextWindowSize", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ToneOptions_ValidConfig_ShouldPass()
    {
        // Arrange
        var validOptions = new ToneMatchingOptions
        {
            EnableEmotionBasedAdaptation = true,
            EnableEscalationDetection = true,
            FrustrationEscalationThreshold = 0.7,
            CacheDurationMinutes = 10,
            DefaultPronoun = "chị"
        };

        var validator = new ValidateToneMatchingOptions();

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Configuration_ToneOptions_InvalidEscalationThreshold_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ToneMatchingOptions
        {
            FrustrationEscalationThreshold = 2.0 // > 1.0
        };

        var validator = new ValidateToneMatchingOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("FrustrationEscalationThreshold", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ToneOptions_NegativeCacheDuration_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ToneMatchingOptions
        {
            CacheDurationMinutes = -5
        };

        var validator = new ValidateToneMatchingOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("CacheDurationMinutes", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ToneOptions_InvalidPronoun_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ToneMatchingOptions
        {
            DefaultPronoun = "invalid"
        };

        var validator = new ValidateToneMatchingOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("DefaultPronoun", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ConversationOptions_ValidConfig_ShouldPass()
    {
        // Arrange
        var validOptions = new ConversationAnalysisOptions
        {
            EnablePatternDetection = true,
            EnableTopicAnalysis = true,
            EnableInsightGeneration = true,
            AnalysisWindowSize = 10,
            BuyingSignalThreshold = 0.7,
            RepeatQuestionThreshold = 0.6,
            RepeatQuestionWindow = 5,
            CacheDurationMinutes = 5
        };

        var validator = new ValidateConversationAnalysisOptions();

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Configuration_ConversationOptions_InvalidAnalysisWindow_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ConversationAnalysisOptions
        {
            AnalysisWindowSize = 0 // < 1
        };

        var validator = new ValidateConversationAnalysisOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("AnalysisWindowSize", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ConversationOptions_InvalidBuyingSignal_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ConversationAnalysisOptions
        {
            BuyingSignalThreshold = 1.5 // > 1.0
        };

        var validator = new ValidateConversationAnalysisOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("BuyingSignalThreshold", result.FailureMessage);
    }

    [Fact]
    public void Configuration_SmallTalkOptions_ValidConfig_ShouldPass()
    {
        // Arrange
        var validOptions = new SmallTalkOptions
        {
            EnableSmallTalkDetection = true,
            SmallTalkConfidenceThreshold = 0.7,
            MaxSmallTalkTurns = 3
        };

        var validator = new ValidateSmallTalkOptions();

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Configuration_SmallTalkOptions_InvalidConfidence_ShouldFail()
    {
        // Arrange
        var invalidOptions = new SmallTalkOptions
        {
            SmallTalkConfidenceThreshold = -0.5
        };

        var validator = new ValidateSmallTalkOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("SmallTalkConfidenceThreshold", result.FailureMessage);
    }

    [Fact]
    public void Configuration_SmallTalkOptions_InvalidMaxTurns_ShouldFail()
    {
        // Arrange
        var invalidOptions = new SmallTalkOptions
        {
            MaxSmallTalkTurns = 0 // < 1
        };

        var validator = new ValidateSmallTalkOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MaxSmallTalkTurns", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ValidationOptions_ValidConfig_ShouldPass()
    {
        // Arrange
        var validOptions = new ResponseValidationOptions
        {
            EnableValidation = true,
            MinResponseLength = 10,
            MaxResponseLength = 500
        };

        var validator = new ValidateResponseValidationOptions();

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Configuration_ValidationOptions_InvalidMinLength_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ResponseValidationOptions
        {
            MinResponseLength = -1
        };

        var validator = new ValidateResponseValidationOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MinResponseLength", result.FailureMessage);
    }

    [Fact]
    public void Configuration_ValidationOptions_MaxLessThanMin_ShouldFail()
    {
        // Arrange
        var invalidOptions = new ResponseValidationOptions
        {
            MinResponseLength = 100,
            MaxResponseLength = 50 // Max < Min
        };

        var validator = new ValidateResponseValidationOptions();

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MaxResponseLength", result.FailureMessage);
    }

    [Fact]
    public void Configuration_AllOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange - Create all options with default values
        var emotionOptions = new EmotionDetectionOptions();
        var toneOptions = new ToneMatchingOptions();
        var conversationOptions = new ConversationAnalysisOptions();
        var smallTalkOptions = new SmallTalkOptions();
        var validationOptions = new ResponseValidationOptions();

        // Act - Validate all
        var emotionResult = new ValidateEmotionDetectionOptions().Validate(null, emotionOptions);
        var toneResult = new ValidateToneMatchingOptions().Validate(null, toneOptions);
        var conversationResult = new ValidateConversationAnalysisOptions().Validate(null, conversationOptions);
        var smallTalkResult = new ValidateSmallTalkOptions().Validate(null, smallTalkOptions);
        var validationResult = new ValidateResponseValidationOptions().Validate(null, validationOptions);

        // Assert - All defaults should be valid
        Assert.True(emotionResult.Succeeded, "EmotionDetectionOptions defaults invalid");
        Assert.True(toneResult.Succeeded, "ToneMatchingOptions defaults invalid");
        Assert.True(conversationResult.Succeeded, "ConversationAnalysisOptions defaults invalid");
        Assert.True(smallTalkResult.Succeeded, "SmallTalkOptions defaults invalid");
        Assert.True(validationResult.Succeeded, "ResponseValidationOptions defaults invalid");
    }

    [Fact]
    public void Configuration_FeatureFlags_DisabledServices_ShouldBeValid()
    {
        // Arrange - All services disabled
        var emotionOptions = new EmotionDetectionOptions { EnableContextAnalysis = false };
        var toneOptions = new ToneMatchingOptions { EnableEmotionBasedAdaptation = false };
        var conversationOptions = new ConversationAnalysisOptions { EnablePatternDetection = false };
        var smallTalkOptions = new SmallTalkOptions { EnableSmallTalkDetection = false };
        var validationOptions = new ResponseValidationOptions { EnableValidation = false };

        // Act - Validate all
        var emotionResult = new ValidateEmotionDetectionOptions().Validate(null, emotionOptions);
        var toneResult = new ValidateToneMatchingOptions().Validate(null, toneOptions);
        var conversationResult = new ValidateConversationAnalysisOptions().Validate(null, conversationOptions);
        var smallTalkResult = new ValidateSmallTalkOptions().Validate(null, smallTalkOptions);
        var validationResult = new ValidateResponseValidationOptions().Validate(null, validationOptions);

        // Assert - Disabled services should still be valid
        Assert.True(emotionResult.Succeeded);
        Assert.True(toneResult.Succeeded);
        Assert.True(conversationResult.Succeeded);
        Assert.True(smallTalkResult.Succeeded);
        Assert.True(validationResult.Succeeded);
    }
}
