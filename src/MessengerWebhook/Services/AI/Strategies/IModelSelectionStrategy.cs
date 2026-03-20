using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.AI.Strategies;

public interface IModelSelectionStrategy
{
    GeminiModelType SelectModel(string message);
}
