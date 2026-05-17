using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Routing;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.Cache;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.Tone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Sales.Reply;

/// <summary>
/// Smoke tests for SalesReplyOrchestrator wiring (R-04).
/// Behavioral coverage of the pipeline lives in handler-level tests (845+ tests),
/// which exercise the orchestrator transitively through SalesStateHandlerBase subclasses.
/// </summary>
public class SalesReplyOrchestratorTests
{
    [Fact]
    public void Constructor_AcceptsAllDependencies()
    {
        var orchestrator = new SalesReplyOrchestrator(
            Mock.Of<IGeminiService>(),
            Mock.Of<ILlmRoutingService>(),
            ragService: null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<IConversationContextAnalyzer>(),
            Mock.Of<ISmallTalkService>(),
            Mock.Of<IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ICustomerIntelligenceService>(),
            Mock.Of<IProductGroundingService>(),
            Mock.Of<ISalesContextResolver>(),
            new SalesPromptBuilder(),
            Mock.Of<ISemanticAnswerCache>(),
            Mock.Of<ITenantContext>(),
            Options.Create(new SalesBotOptions { ConversationHistoryLimit = 15 }),
            Options.Create(new RAGOptions { Enabled = false, TopK = 5 }),
            NullLogger<SalesReplyOrchestrator>.Instance);

        orchestrator.Should().NotBeNull();
        orchestrator.Should().BeAssignableTo<ISalesReplyOrchestrator>();
    }

    [Fact]
    public void Constructor_AcceptsNullRagService()
    {
        var orchestrator = new SalesReplyOrchestrator(
            Mock.Of<IGeminiService>(),
            Mock.Of<ILlmRoutingService>(),
            ragService: null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<IConversationContextAnalyzer>(),
            Mock.Of<ISmallTalkService>(),
            Mock.Of<IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ICustomerIntelligenceService>(),
            Mock.Of<IProductGroundingService>(),
            Mock.Of<ISalesContextResolver>(),
            new SalesPromptBuilder(),
            Mock.Of<ISemanticAnswerCache>(),
            Mock.Of<ITenantContext>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            NullLogger<SalesReplyOrchestrator>.Instance);

        orchestrator.Should().NotBeNull();
    }
}
