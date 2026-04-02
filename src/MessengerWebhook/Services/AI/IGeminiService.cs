using MessengerWebhook.Models;
using MessengerWebhook.Services.AI.Models;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.AI;

public interface IGeminiService
{
    Task<string> SendMessageAsync(
        string userId,
        string message,
        List<AiConversationMessage> history,
        GeminiModelType? modelOverride = null,
        string? ragContext = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamMessageAsync(
        string userId,
        string message,
        List<AiConversationMessage> history,
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

    /// <summary>
    /// Detects customer intent from message using AI reasoning.
    /// Uses Gemini FlashLite for fast, context-aware classification into 5 categories:
    /// Browsing, Consulting, ReadyToBuy, Confirming, Questioning.
    /// Expected latency: <500ms (p95).
    /// </summary>
    /// <param name="message">Customer message to classify</param>
    /// <param name="currentState">Current conversation state for context</param>
    /// <param name="hasProduct">Whether customer has selected a product</param>
    /// <param name="hasContact">Whether customer has provided contact info</param>
    /// <param name="recentHistory">Recent conversation history (last 3 messages) for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Intent detection result with confidence score and reasoning</returns>
    Task<IntentDetectionResult> DetectIntentAsync(
        string message,
        ConversationState currentState,
        bool hasProduct,
        bool hasContact,
        List<AiConversationMessage>? recentHistory = null,
        CancellationToken cancellationToken = default);
}
