using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Configuration;

public class ValidatePolicyGuardOptionsTests
{
    private readonly ValidatePolicyGuardOptions _validator = new();

    [Fact]
    public void Validate_WhenSafeReplyMessageIsPresent_ReturnsSuccess()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenSafeReplyMessageIsWhitespace_ReturnsFailure()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions { SafeReplyMessage = "   " });

        AssertFailure("PolicyGuard:SafeReplyMessage must not be empty.", result);
    }

    [Fact]
    public void Validate_WhenSemanticClassifierEnabledWithoutGeminiApiKey_ReturnsFailure()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions { EnableSemanticClassifier = true });

        AssertFailure("PolicyGuard:EnableSemanticClassifier requires Gemini:ApiKey.", result);
    }

    [Fact]
    public void Validate_WhenSemanticClassifierEnabledWithGeminiApiKey_ReturnsSuccess()
    {
        var validator = new ValidatePolicyGuardOptions(Options.Create(new GeminiOptions { ApiKey = "test-key" }));

        var result = validator.Validate(null, new PolicyGuardOptions { EnableSemanticClassifier = true });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenUnitIntervalValueIsOutOfRange_ReturnsFailure()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions { RepeatMentionBoost = 1.1m });

        AssertFailure("PolicyGuard:RepeatMentionBoost must be between 0 and 1.", result);
    }

    [Fact]
    public void Validate_WhenThresholdsAreOutOfOrder_ReturnsFailure()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions
        {
            SafeReplyThreshold = 0.7m,
            SoftEscalateThreshold = 0.6m,
            HardEscalateThreshold = 0.5m
        });

        AssertFailure("PolicyGuard:SafeReplyThreshold must be less than or equal to SoftEscalateThreshold.", result);
        AssertFailure("PolicyGuard:SoftEscalateThreshold must be less than or equal to HardEscalateThreshold.", result);
    }

    [Fact]
    public void Validate_WhenMaxRecentTurnsIsZero_ReturnsFailure()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions { MaxRecentTurns = 0 });

        AssertFailure("PolicyGuard:MaxRecentTurns must be greater than 0.", result);
    }

    [Fact]
    public void Validate_WhenClassifierTimeoutIsZero_ReturnsFailure()
    {
        var result = _validator.Validate(null, new PolicyGuardOptions { ClassifierTimeoutMs = 0 });

        AssertFailure("PolicyGuard:ClassifierTimeoutMs must be greater than 0.", result);
    }

    private static void AssertFailure(string expected, ValidateOptionsResult result)
    {
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(expected, result.Failures!);
    }
}
