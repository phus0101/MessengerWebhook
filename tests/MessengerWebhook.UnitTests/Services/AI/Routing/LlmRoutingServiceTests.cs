using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.AI.Routing;
using MessengerWebhook.Services.Sales.Intent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Services.AI.Routing;

public class LlmRoutingServiceTests
{
    private static LlmRoutingService Build(LlmRoutingOptions? options = null)
    {
        var opts = Options.Create(options ?? new LlmRoutingOptions());
        return new LlmRoutingService(opts, NullLogger<LlmRoutingService>.Instance);
    }

    private static LlmRoutingContext ChatCtx(
        bool isVip = false,
        decimal? ticket = null,
        float confidence = 1f,
        int turns = 0) => new()
    {
        Purpose = "chat",
        IsVipCustomer = isVip,
        EstimatedTicketValue = ticket,
        Intent = new CommerceMsgIntent { Confidence = confidence },
        HistoryTurnCount = turns
    };

    // ── Purpose routing ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("classify")]
    [InlineData("summarize")]
    public void SelectModel_ClassifyOrSummarize_ReturnsFlashLite(string purpose)
    {
        var svc = Build();
        var result = svc.SelectModel(new LlmRoutingContext { Purpose = purpose });
        result.Should().Be(GeminiModelType.FlashLite);
    }

    [Fact]
    public void SelectModel_ChatPurpose_DoesNotReturnFlashLite_ByDefault()
    {
        var svc = Build();
        var result = svc.SelectModel(ChatCtx());
        result.Should().NotBe(GeminiModelType.FlashLite);
    }

    // ── Default chat → Flash ─────────────────────────────────────────────────

    [Fact]
    public void SelectModel_DefaultChat_ReturnsFlash()
    {
        var svc = Build();
        var result = svc.SelectModel(ChatCtx());
        result.Should().Be(GeminiModelType.Flash);
    }

    // ── VIP trigger ──────────────────────────────────────────────────────────

    [Fact]
    public void SelectModel_VipCustomer_ReturnsPro()
    {
        var svc = Build();
        var result = svc.SelectModel(ChatCtx(isVip: true));
        result.Should().Be(GeminiModelType.Pro);
    }

    // ── Ticket value trigger ──────────────────────────────────────────────────

    [Fact]
    public void SelectModel_TicketAtThreshold_ReturnsPro()
    {
        var svc = Build(new LlmRoutingOptions { ProTierMinTicketValueVnd = 1_000_000 });
        var result = svc.SelectModel(ChatCtx(ticket: 1_000_000m));
        result.Should().Be(GeminiModelType.Pro);
    }

    [Fact]
    public void SelectModel_TicketBelowThreshold_ReturnsFlash()
    {
        var svc = Build(new LlmRoutingOptions { ProTierMinTicketValueVnd = 1_000_000 });
        var result = svc.SelectModel(ChatCtx(ticket: 999_999m));
        result.Should().Be(GeminiModelType.Flash);
    }

    // ── Low confidence trigger ────────────────────────────────────────────────

    [Fact]
    public void SelectModel_LowConfidence_ReturnsPro()
    {
        var svc = Build(new LlmRoutingOptions { LowConfidenceThreshold = 0.6f });
        var result = svc.SelectModel(ChatCtx(confidence: 0.5f));
        result.Should().Be(GeminiModelType.Pro);
    }

    [Fact]
    public void SelectModel_ConfidenceAtThreshold_ReturnsFlash()
    {
        // confidence == threshold is NOT below threshold, so should be Flash
        var svc = Build(new LlmRoutingOptions { LowConfidenceThreshold = 0.6f });
        var result = svc.SelectModel(ChatCtx(confidence: 0.6f));
        result.Should().Be(GeminiModelType.Flash);
    }

    // ── Long conversation trigger ─────────────────────────────────────────────

    [Fact]
    public void SelectModel_LongHistory_ReturnsPro()
    {
        var svc = Build(new LlmRoutingOptions { LongConversationThreshold = 8 });
        var result = svc.SelectModel(ChatCtx(turns: 9));
        result.Should().Be(GeminiModelType.Pro);
    }

    [Fact]
    public void SelectModel_HistoryAtThreshold_ReturnsFlash()
    {
        // turns == threshold is NOT above threshold
        var svc = Build(new LlmRoutingOptions { LongConversationThreshold = 8 });
        var result = svc.SelectModel(ChatCtx(turns: 8));
        result.Should().Be(GeminiModelType.Flash);
    }

    // ── Disabled routing ──────────────────────────────────────────────────────

    [Fact]
    public void SelectModel_Disabled_ClassifyReturnsFlashLite()
    {
        var svc = Build(new LlmRoutingOptions { Enabled = false });
        var result = svc.SelectModel(new LlmRoutingContext { Purpose = "classify" });
        result.Should().Be(GeminiModelType.FlashLite);
    }

    [Fact]
    public void SelectModel_Disabled_ChatReturnsFlash()
    {
        var svc = Build(new LlmRoutingOptions { Enabled = false });
        // Even VIP should be ignored when routing is disabled
        var result = svc.SelectModel(ChatCtx(isVip: true));
        result.Should().Be(GeminiModelType.Flash);
    }

    // ── Null intent (no Confidence field) ────────────────────────────────────

    [Fact]
    public void SelectModel_NullIntent_DefaultsToFlash()
    {
        var svc = Build();
        var result = svc.SelectModel(new LlmRoutingContext { Purpose = "chat" });
        result.Should().Be(GeminiModelType.Flash);
    }
}
