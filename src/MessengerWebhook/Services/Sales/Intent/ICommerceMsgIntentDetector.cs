using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.Sales.Intent;

/// <summary>
/// Detects and aggregates all commerce intent signals for a single incoming message.
/// Separates keyword detection (sync, cheap) from AI merging (async, expensive).
/// </summary>
public interface ICommerceMsgIntentDetector
{
    /// <summary>
    /// Runs keyword-based detection synchronously.
    /// Does not call any AI service.
    /// </summary>
    CommerceMsgIntent DetectFromKeywords(
        string message,
        StateContext ctx,
        bool hasProduct,
        bool hasContact);

    /// <summary>
    /// Merges AI intent results into an existing keyword intent snapshot.
    /// Returns a new record (non-destructive — original is unchanged).
    /// </summary>
    Task<CommerceMsgIntent> MergeWithAiIntentAsync(
        CommerceMsgIntent keywords,
        IntentDetectionResult aiIntent,
        SubIntentResult? subIntent,
        float confidenceThreshold);
}
