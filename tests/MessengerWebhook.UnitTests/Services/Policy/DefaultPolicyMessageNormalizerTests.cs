using MessengerWebhook.Services.Policy;

namespace MessengerWebhook.UnitTests.Services.Policy;

public class DefaultPolicyMessageNormalizerTests
{
    private readonly DefaultPolicyMessageNormalizer _normalizer = new();

    [Fact]
    public void Normalize_RemovesVietnameseDiacriticsAndCollapsesWhitespace()
    {
        var normalized = _normalizer.Normalize("  Hoàn   tiền   ");

        Assert.Equal("hoan tien", normalized);
    }

    [Fact]
    public void Normalize_ReplacesNoiseSeparatorsAndLeetspeak()
    {
        var normalized = _normalizer.Normalize("h0an---tien");

        Assert.Equal("hoan tien", normalized);
    }

    [Fact]
    public void Normalize_PromptInjectionVariant_ReturnsCanonicalText()
    {
        var normalized = _normalizer.Normalize("pr0mpt_injecti0n");

        Assert.Equal("prompt injection", normalized);
    }
}
