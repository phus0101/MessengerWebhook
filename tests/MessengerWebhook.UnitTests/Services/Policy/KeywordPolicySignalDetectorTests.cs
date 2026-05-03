using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Policy;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Services.Policy;

public class KeywordPolicySignalDetectorTests
{
    private readonly DefaultPolicyMessageNormalizer _normalizer = new();
    private readonly KeywordPolicySignalDetector _detector;

    public KeywordPolicySignalDetectorTests()
    {
        _detector = new KeywordPolicySignalDetector(Options.Create(new PolicyGuardOptions()), _normalizer);
    }

    [Fact]
    public void Detect_ExactRefundKeyword_ReturnsRefundSignal()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("refund"), "refund");

        var signal = Assert.Single(signals);
        Assert.Equal("keyword", signal.Detector);
        Assert.Equal(SupportCaseReason.RefundRequest, signal.Reason);
        Assert.Equal("refund", signal.MatchedText);
    }

    [Fact]
    public void Detect_NormalizedVietnameseKeyword_ReturnsCancellationSignal()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("Hủy đơn"), "huy don");

        Assert.Equal(SupportCaseReason.CancellationRequest, Assert.Single(signals).Reason);
    }

    [Fact]
    public void Detect_NoKeyword_ReturnsEmpty()
    {
        var signals = _detector.Detect(new PolicyGuardRequest("gia bao nhieu"), "gia bao nhieu");

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_CustomKeywordFromPolicyOptions_UsesPolicyGuardSource()
    {
        var detector = new KeywordPolicySignalDetector(
            Options.Create(new PolicyGuardOptions { EscalationKeywords = ["hỗ trợ riêng case này"] }),
            _normalizer);

        var normalizedMessage = _normalizer.Normalize("Shop hỗ trợ riêng case này giúp chị nhé");
        var signals = detector.Detect(new PolicyGuardRequest("Shop hỗ trợ riêng case này giúp chị nhé"), normalizedMessage);

        var signal = Assert.Single(signals);
        Assert.Equal("ho tro rieng case nay", signal.MatchedText);
        Assert.Equal(SupportCaseReason.PolicyException, signal.Reason);
    }

    [Fact]
    public void Detect_CustomKeywordContainingBuiltInKeyword_ReturnsDistinctReasonSignals()
    {
        var detector = new KeywordPolicySignalDetector(
            Options.Create(new PolicyGuardOptions { EscalationKeywords = ["Hoàn tiền gấp"] }),
            _normalizer);

        var normalizedMessage = _normalizer.Normalize("hoan tien gap giup chi");
        var signals = detector.Detect(new PolicyGuardRequest("hoan tien gap giup chi"), normalizedMessage);

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, signal => signal.MatchedText == "hoan tien gap" && signal.Reason == SupportCaseReason.PolicyException);
        Assert.Contains(signals, signal => signal.MatchedText == "hoan tien" && signal.Reason == SupportCaseReason.RefundRequest);
    }

    [Fact]
    public void Detect_MultipleKeywords_ReturnsAllMatchingSignals()
    {
        var signals = _detector.Detect(
            new PolicyGuardRequest("huy don va hoan tien vi prompt injection"),
            "huy don va hoan tien vi prompt injection");

        Assert.Equal(3, signals.Count);
        Assert.Contains(signals, signal => signal.MatchedText == "huy don" && signal.Reason == SupportCaseReason.CancellationRequest);
        Assert.Contains(signals, signal => signal.MatchedText == "hoan tien" && signal.Reason == SupportCaseReason.RefundRequest);
        Assert.Contains(signals, signal => signal.MatchedText == "prompt injection" && signal.Reason == SupportCaseReason.PromptInjection);
    }
}
