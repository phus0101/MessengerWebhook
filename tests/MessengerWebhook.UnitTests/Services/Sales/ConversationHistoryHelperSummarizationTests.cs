using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Sales;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.StateMachine.Models;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Sales;

public class ConversationHistoryHelperSummarizationTests
{
    // ── AddToHistoryWithSummaryAsync ──────────────────────────────────────────

    [Fact]
    public async Task AddToHistoryWithSummaryAsync_BelowThreshold_DoesNotSummarize()
    {
        var summarizer = new Mock<IConversationSummarizer>();
        var ctx = new StateContext { FacebookPSID = "psid-1" };

        // Add 5 turns (threshold=10, ephemeral=6)
        for (var i = 0; i < 5; i++)
            ConversationHistoryHelper.AddToHistory(ctx, "user", $"msg {i}", 20);

        await ConversationHistoryHelper.AddToHistoryWithSummaryAsync(
            ctx, "user", "new message",
            historyLimit: 20,
            ephemeralWindowSize: 6,
            summarizationThreshold: 10,
            summarizer: summarizer.Object);

        summarizer.Verify(s => s.SummarizeAsync(
            It.IsAny<IReadOnlyList<ConversationMessage>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        var history = ConversationHistoryHelper.GetHistory(ctx);
        Assert.Equal(6, history.Count); // 5 + 1 new
    }

    [Fact]
    public async Task AddToHistoryWithSummaryAsync_ExceedsThreshold_SummarizesAndTrimsHistory()
    {
        var summarizer = new Mock<IConversationSummarizer>();
        summarizer
            .Setup(s => s.SummarizeAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Sản phẩm quan tâm: KCN\nIntent khách: muốn mua");

        var ctx = new StateContext { FacebookPSID = "psid-2" };

        // Add 10 turns to hit threshold (threshold=10, so count >10 triggers summarization)
        for (var i = 0; i < 10; i++)
            ConversationHistoryHelper.AddToHistory(ctx, "user", $"msg {i}", 20);

        // Adding the 11th triggers summarization
        await ConversationHistoryHelper.AddToHistoryWithSummaryAsync(
            ctx, "user", "msg 10",
            historyLimit: 20,
            ephemeralWindowSize: 6,
            summarizationThreshold: 10,
            summarizer: summarizer.Object);

        summarizer.Verify(s => s.SummarizeAsync(
            It.IsAny<IReadOnlyList<ConversationMessage>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // History trimmed to ephemeral window
        var history = ConversationHistoryHelper.GetHistory(ctx);
        Assert.Equal(6, history.Count);

        // Summary stored
        var summary = ctx.GetData<string>("conversationSummary");
        Assert.Equal("Sản phẩm quan tâm: KCN\nIntent khách: muốn mua", summary);
    }

    [Fact]
    public async Task AddToHistoryWithSummaryAsync_PassesExistingSummaryToSummarizer()
    {
        var summarizer = new Mock<IConversationSummarizer>();
        summarizer
            .Setup(s => s.SummarizeAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), "prior summary", It.IsAny<CancellationToken>()))
            .ReturnsAsync("updated summary");

        var ctx = new StateContext { FacebookPSID = "psid-3" };
        ctx.SetData("conversationSummary", "prior summary");

        for (var i = 0; i < 10; i++)
            ConversationHistoryHelper.AddToHistory(ctx, "user", $"msg {i}", 20);

        await ConversationHistoryHelper.AddToHistoryWithSummaryAsync(
            ctx, "user", "msg 10",
            historyLimit: 20,
            ephemeralWindowSize: 6,
            summarizationThreshold: 10,
            summarizer: summarizer.Object);

        summarizer.Verify(s => s.SummarizeAsync(
            It.IsAny<IReadOnlyList<ConversationMessage>>(),
            "prior summary",
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal("updated summary", ctx.GetData<string>("conversationSummary"));
    }

    // ── BuildConversationSummarySection ──────────────────────────────────────

    [Fact]
    public void BuildConversationSummarySection_NoSummary_ReturnsEmptyString()
    {
        var builder = new SalesPromptBuilder();
        var ctx = new StateContext { FacebookPSID = "psid-4" };

        var result = builder.BuildConversationSummarySection(ctx);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildConversationSummarySection_WithSummary_ReturnsFormattedSection()
    {
        var builder = new SalesPromptBuilder();
        var ctx = new StateContext { FacebookPSID = "psid-5" };
        ctx.SetData("conversationSummary", "Sản phẩm quan tâm: KCN");

        var result = builder.BuildConversationSummarySection(ctx);

        Assert.StartsWith("[BỐI CẢNH PHIÊN]", result);
        Assert.Contains("Sản phẩm quan tâm: KCN", result);
    }

    [Fact]
    public void BuildConversationSummarySection_WhitespaceSummary_ReturnsEmptyString()
    {
        var builder = new SalesPromptBuilder();
        var ctx = new StateContext { FacebookPSID = "psid-6" };
        ctx.SetData("conversationSummary", "   ");

        var result = builder.BuildConversationSummarySection(ctx);

        Assert.Equal(string.Empty, result);
    }
}
