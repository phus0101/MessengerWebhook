using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Policy;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Services.Policy;

public class FuzzyPolicySignalDetectorTests
{
    private readonly FuzzyPolicySignalDetector _detector = new(Options.Create(new PolicyGuardOptions()));

    [Fact]
    public void Detect_LightTypoRefundPhrase_ReturnsRefundSignal()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("hoan tieng"), "hoan tieng");

        var signal = Assert.Single(signals);
        Assert.Equal("fuzzy", signal.Detector);
        Assert.Equal(SupportCaseReason.RefundRequest, signal.Reason);
    }

    [Fact]
    public void Detect_ExactPromptInjection_ReturnsPromptInjectionSignal()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("prompt injection"), "prompt injection");

        Assert.Equal(SupportCaseReason.PromptInjection, Assert.Single(signals).Reason);
    }

    [Fact]
    public void Detect_UnrelatedPhrase_ReturnsEmpty()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("gia bao nhieu"), "gia bao nhieu");

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_NearMissBelowThreshold_ReturnsEmpty()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("hoan tixx"), "hoan tixx");

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_LightTypoRefundPhraseInsideSentence_ReturnsRefundSignal()
    {
        var signals = _detector.Detect(
            new PolicyGuardRequest("chi muon hoan tieng giup chi"),
            "chi muon hoan tieng giup chi");

        var signal = Assert.Single(signals);
        Assert.Equal(SupportCaseReason.RefundRequest, signal.Reason);
    }
}
