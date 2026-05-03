using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Configuration;

public class ValidateGeminiOptionsTests
{
    private readonly ValidateGeminiOptions _validator = new();

    [Fact]
    public void Validate_WhenAiDetectionTimeoutIsPositive_ReturnsSuccess()
    {
        var result = _validator.Validate(null, new GeminiOptions { AiDetectionTimeoutMs = 500, TimeoutSeconds = 60, MaxRetries = 3 });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAiDetectionTimeoutIsZero_ReturnsFailure()
    {
        var result = _validator.Validate(null, new GeminiOptions { AiDetectionTimeoutMs = 0, TimeoutSeconds = 60, MaxRetries = 3 });

        Assert.False(result.Succeeded);
        var failures = result.Failures ?? Array.Empty<string>();
        Assert.Contains("Gemini:AiDetectionTimeoutMs must be greater than 0.", failures);
    }

}
