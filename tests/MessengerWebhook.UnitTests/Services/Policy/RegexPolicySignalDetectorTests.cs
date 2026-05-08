using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Policy;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Services.Policy;

public class RegexPolicySignalDetectorTests
{
    private readonly RegexPolicySignalDetector _detector = new(Options.Create(new PolicyGuardOptions()));

    [Fact]
    public void Detect_RefundPhraseWithCollapsedSpacing_ReturnsRefundSignal()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("hoan     tien"), "hoan     tien");

        var signal = Assert.Single(signals);
        Assert.Equal("regex", signal.Detector);
        Assert.Equal(SupportCaseReason.RefundRequest, signal.Reason);
    }

    [Fact]
    public void Detect_ObfuscatedPolicyException_ReturnsPolicyExceptionSignal()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("mien phi van chuyen"), "mien phi van chuyen");

        Assert.Equal(SupportCaseReason.PolicyException, Assert.Single(signals).Reason);
    }

    [Fact]
    public void Detect_NoPattern_ReturnsEmpty()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("gia sao em"), "gia sao em");

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_SeparatorHeavyPromptVariant_ReturnsEmpty()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("p r o m p t i n j e c t i o n"), "p r o m p t i n j e c t i o n");

        Assert.Empty(signals);
    }
}
