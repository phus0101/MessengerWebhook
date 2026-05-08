using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Policy;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Policy;

public class PolicyGuardServiceTests
{
    private readonly DefaultPolicyMessageNormalizer _normalizer = new();

    [Fact]
    public void Evaluate_ExactKeywordHit_HardEscalatesForCompatibility()
    {
        var service = CreateService();

        var decision = service.Evaluate("hoan tien");

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.HardEscalate, decision.Action);
        Assert.Equal(SupportCaseReason.RefundRequest, decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_NoDiacriticRefundHit_Escalates()
    {
        var service = CreateService();

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("Hoàn tiền giúp chị"));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(SupportCaseReason.RefundRequest, decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_ObfuscatedPromptInjection_Escalates()
    {
        var service = CreateService();

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("pr0mpt_inject!!on"));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.HardEscalate, decision.Action);
        Assert.Equal(SupportCaseReason.PromptInjection, decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_UnsupportedQuestionAtBoundary_ReturnsSafeReply()
    {
        var options = new PolicyGuardOptions
        {
            EnableRegexDetector = false,
            EnableFuzzyDetector = false,
            SafeReplyThreshold = 0.35m,
            SoftEscalateThreshold = 0.60m,
            HardEscalateThreshold = 0.80m
        };
        var signal = new PolicySignal("semantic", "UnsupportedQuestion", "hoi lai", 1m, 0.72m, SupportCaseReason.UnsupportedQuestion, "classifier");
        var detector = new Mock<IPolicySignalDetector>();
        detector.Setup(x => x.Detect(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>())).Returns([signal]);
        var service = CreateService(options, [detector.Object]);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("hoi lai"));

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.SafeReply, decision.Action);
        Assert.Equal(SupportCaseReason.UnsupportedQuestion, decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_NegativePhrase_DoesNotEscalate()
    {
        var service = CreateService();

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("chi muon hoi gia san pham"));

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.Allow, decision.Action);
    }

    [Fact]
    public async Task EvaluateAsync_OpenSupportCaseBoost_CrossesSoftThreshold()
    {
        var options = new PolicyGuardOptions
        {
            EnableRegexDetector = false,
            EnableFuzzyDetector = false,
            SafeReplyThreshold = 0.35m,
            SoftEscalateThreshold = 0.60m,
            HardEscalateThreshold = 0.80m,
            OpenSupportCaseBoost = 0.15m
        };
        var signal = new PolicySignal("semantic", "UnsupportedQuestion", "hoi lai", 1m, 0.72m, SupportCaseReason.UnsupportedQuestion, "classifier");
        var detector = new Mock<IPolicySignalDetector>();
        detector.Setup(x => x.Detect(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>())).Returns([signal]);

        var service = CreateService(options, [detector.Object]);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("hoi lai", HasOpenSupportCase: true));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.SoftEscalate, decision.Action);
        Assert.Equal(SupportCaseReason.UnsupportedQuestion, decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_UsesSemanticClassifierMetadata_WhenEnabledAndNoSignals()
    {
        var options = new PolicyGuardOptions
        {
            EnableRegexDetector = false,
            EnableFuzzyDetector = false,
            EnableSemanticClassifier = true
        };
        var classifier = new Mock<IPolicyIntentClassifier>();
        classifier
            .Setup(x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyClassificationResult(
                PolicySemanticIntent.ManualReview,
                0.81m,
                SupportCaseReason.ManualReview,
                "semantic",
                []));

        var service = CreateService(options, [], classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("can gap nguoi that tu van"));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.SoftEscalate, decision.Action);
        Assert.Equal(SupportCaseReason.ManualReview, decision.Reason);
        Assert.Equal("semantic", decision.Summary);
        Assert.Equal(0.81m, decision.Confidence);
        Assert.True(decision.SemanticClassifierAttempted);
        classifier.Verify(x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_BelowSafeReplyThreshold_ReturnsAllow()
    {
        var options = new PolicyGuardOptions
        {
            EnableRegexDetector = false,
            EnableFuzzyDetector = false,
            SafeReplyThreshold = 0.35m,
            SoftEscalateThreshold = 0.60m,
            HardEscalateThreshold = 0.80m
        };
        var signal = new PolicySignal("semantic", "UnsupportedQuestion", "hoi lai", 0.75m, 0.72m, SupportCaseReason.UnsupportedQuestion, "classifier");
        var detector = new Mock<IPolicySignalDetector>();
        detector.Setup(x => x.Detect(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>())).Returns([signal]);
        var service = CreateService(options, [detector.Object]);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("hoi lai"));

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.Allow, decision.Action);
        Assert.Equal(SupportCaseReason.UnsupportedQuestion, decision.Reason);
    }

    [Fact]
    public void Evaluate_SyncPath_IgnoresSemanticClassifier()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = true,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false
        };
        var classifier = new Mock<IPolicyIntentClassifier>(MockBehavior.Strict);
        var service = CreateService(options, [], classifier.Object);

        var decision = service.Evaluate("can nhan vien ho tro");

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.Allow, decision.Action);
        Assert.False(decision.SemanticClassifierAttempted);
        classifier.Verify(
            x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_SemanticClassifierDisabled_DoesNotCallClassifier()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = false,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false
        };
        var classifier = new Mock<IPolicyIntentClassifier>(MockBehavior.Strict);
        var service = CreateService(options, [], classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("can nhan vien ho tro"));

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.Allow, decision.Action);
        Assert.False(decision.SemanticClassifierAttempted);
        classifier.Verify(
            x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_NormalSalesMessage_DoesNotCallClassifier()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = true,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false
        };
        var classifier = new Mock<IPolicyIntentClassifier>(MockBehavior.Strict);
        var service = CreateService(options, [], classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("chi muon hoi gia san pham"));

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.Allow, decision.Action);
        Assert.False(decision.SemanticClassifierAttempted);
        classifier.Verify(
            x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_HardKeywordSignal_DoesNotCallClassifier()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = true,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false
        };
        var classifier = new Mock<IPolicyIntentClassifier>(MockBehavior.Strict);
        var service = CreateService(options, null, classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("hoan tien giup chi"));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.HardEscalate, decision.Action);
        Assert.Equal(SupportCaseReason.RefundRequest, decision.Reason);
        Assert.False(decision.SemanticClassifierAttempted);
        classifier.Verify(
            x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_SemanticOnlyManualReview_CapsAtSoftEscalation()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = true,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false,
            SoftEscalateThreshold = 0.60m,
            HardEscalateThreshold = 0.80m
        };
        var classifier = new Mock<IPolicyIntentClassifier>();
        classifier
            .Setup(x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyClassificationResult(
                PolicySemanticIntent.ManualReview,
                1.0m,
                SupportCaseReason.ManualReview,
                "semantic",
                []));
        var service = CreateService(options, [], classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("can gap nguoi that tu van"));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.SoftEscalate, decision.Action);
        Assert.Equal(0.75m, decision.Score);
        Assert.Equal(1.0m, decision.Confidence);
    }

    [Fact]
    public async Task EvaluateAsync_ClassifierReturnsNull_FallsBackToDeterministicSignals()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = true,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false
        };
        var detector = new Mock<IPolicySignalDetector>();
        detector
            .Setup(x => x.Detect(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>()))
            .Returns([new PolicySignal("keyword", "RefundRequest", "hoan tien", 1m, 1m, SupportCaseReason.RefundRequest, "exact keyword")]);
        var classifier = new Mock<IPolicyIntentClassifier>();
        classifier
            .Setup(x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyClassificationResult?)null);
        var service = CreateService(options, [detector.Object], classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("hoan tien giup chi"));

        Assert.True(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.HardEscalate, decision.Action);
        Assert.Equal(SupportCaseReason.RefundRequest, decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_ClassifierThrows_FallsBackToDeterministicSignals()
    {
        var options = new PolicyGuardOptions
        {
            EnableSemanticClassifier = true,
            EnableRegexDetector = false,
            EnableFuzzyDetector = false
        };
        var classifier = new Mock<IPolicyIntentClassifier>();
        classifier
            .Setup(x => x.ClassifyAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("classifier failed"));
        var service = CreateService(options, [], classifier.Object);

        var decision = await service.EvaluateAsync(new PolicyGuardRequest("can gap nguoi that tu van"));

        Assert.False(decision.RequiresEscalation);
        Assert.Equal(PolicyAction.Allow, decision.Action);
        Assert.True(decision.SemanticClassifierAttempted);
    }

    private PolicyGuardService CreateService(
        PolicyGuardOptions? options = null,
        IEnumerable<IPolicySignalDetector>? detectors = null,
        IPolicyIntentClassifier? classifier = null)
    {
        var salesOptions = Options.Create(new SalesBotOptions());
        var policyOptions = Options.Create(options ?? new PolicyGuardOptions());

        return new PolicyGuardService(
            salesOptions,
            policyOptions,
            _normalizer,
            new DefaultPolicyRiskScorer(policyOptions),
            detectors ??
            [
                new KeywordPolicySignalDetector(policyOptions, _normalizer),
                new RegexPolicySignalDetector(policyOptions),
                new FuzzyPolicySignalDetector(policyOptions)
            ],
            classifier);
    }
}
