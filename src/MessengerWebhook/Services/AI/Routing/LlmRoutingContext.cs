using MessengerWebhook.Models;
using MessengerWebhook.Services.Sales.Intent;

namespace MessengerWebhook.Services.AI.Routing;

/// <summary>
/// Carries the signals needed for LLM tier selection.
/// </summary>
public record LlmRoutingContext
{
    public CommerceMsgIntent? Intent { get; init; }
    public ConversationState State { get; init; }
    public int HistoryTurnCount { get; init; }
    public decimal? EstimatedTicketValue { get; init; }
    public bool IsVipCustomer { get; init; }

    /// <summary>
    /// One of: "chat", "classify", "summarize".
    /// FlashLite is forced for "classify" and "summarize".
    /// </summary>
    public string Purpose { get; init; } = "chat";
}
