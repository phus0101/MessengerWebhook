using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.AI.Routing;

/// <summary>
/// Selects the appropriate Gemini model tier based on conversation context.
/// </summary>
public interface ILlmRoutingService
{
    GeminiModelType SelectModel(LlmRoutingContext context);
}
