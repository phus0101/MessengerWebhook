using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.AI;

public interface IGeminiService
{
    Task<string> SendMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default);

    GeminiModelType SelectModel(string message);

    /// <summary>
    /// Detects if a customer message is confirming remembered contact information using AI reasoning.
    /// Uses Gemini FlashLite model for fast, context-aware classification.
    /// Expected latency: <500ms (p95). Results are cached for 5 minutes.
    /// </summary>
    /// <param name="message">Customer message to classify</param>
    /// <param name="contextPhone">Remembered phone number from previous order</param>
    /// <param name="contextAddress">Remembered shipping address from previous order</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detection result with confidence score and reasoning</returns>
    Task<ConfirmationDetectionResult> DetectConfirmationAsync(
        string message,
        string contextPhone,
        string contextAddress,
        CancellationToken cancellationToken = default);
}
