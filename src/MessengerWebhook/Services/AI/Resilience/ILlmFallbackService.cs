using MessengerWebhook.Models;

namespace MessengerWebhook.Services.AI.Resilience;

public interface ILlmFallbackService
{
    bool IsCircuitOpen { get; set; }
    string GetDegradedResponse(ConversationState state);
}
