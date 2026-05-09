using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.Sales.Reply;

/// <summary>
/// Input for SalesReplyOrchestrator.GenerateAsync.
/// StateContext is passed mutable — orchestrator may set conversation/tone/emotion data,
/// matching the historical SalesStateHandlerBase behavior.
/// </summary>
public sealed class SalesReplyRequest
{
    public required StateContext Context { get; init; }
    public required string Message { get; init; }
    public CustomerIntent? Intent { get; init; }
    public SubIntentResult? SubIntent { get; init; }
}
