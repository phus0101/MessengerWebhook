using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.StateMachine.Models;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.Sales.Context;

/// <summary>
/// Resolves sales context from conversation state: products, contact, history recovery.
/// Pure reads + StateContext mutations only — no Messenger sends, no DB writes.
/// </summary>
public interface ISalesContextResolver
{
    Task<VipProfile?> GetVipProfileAsync(StateContext ctx);
    Task<List<Product>> GetActiveSelectedProductsAsync(StateContext ctx);
    Task<Product?> GetActiveProductOrResolveAsync(StateContext ctx, string message);
    Task<Product?> ResolveCurrentProductAsync(StateContext ctx, string message);
    Task ApplyResolvedProductAsync(StateContext ctx, Product product, string source);
    Task TryExtractProductFromHistoryAsync(StateContext ctx, string? currentMessage = null);
    Task<Product?> TryResolveNumberedSuggestionSelectionAsync(StateContext ctx, string? currentMessage);
    Task<List<HistoryProductCandidate>> CollectHistoryProductCandidatesAsync(
        List<AiConversationMessage> recentMessages, string role);
    Task<HistoryProductCandidate?> ResolveAmbiguousHistoryProductCandidateAsync(
        StateContext ctx,
        List<AiConversationMessage> recentMessages,
        List<HistoryProductCandidate> candidates,
        string preferredRole);
    Task<CommercialFactSnapshot?> BuildCommercialFactSnapshotAsync(StateContext ctx, Product product);
    Task<CommercialFactSnapshot?> BuildCommercialFactSnapshotForPolicyAsync(StateContext ctx, Product product);
    Task RefreshSelectedProductPolicyContextAsync(StateContext ctx, string message);
    Task SyncActiveProductPolicyContextAsync(StateContext ctx, string productCode);

    /// <summary>Returns true if message is a numbered item selection (e.g. "1", "chọn số 2").</summary>
    bool IsRelatedSuggestionSelection(string message);

    /// <summary>Extracts the selected number from a suggestion selection message, or null if not a selection.</summary>
    int? ExtractRelatedSuggestionSelectionNumber(string? message);
}
