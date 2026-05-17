using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Tone.Models;
using MessengerWebhook.StateMachine.Models;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;
using CustomerIntent = MessengerWebhook.Services.AI.Models.CustomerIntent;

namespace MessengerWebhook.Services.Sales.Prompt;

/// <summary>
/// Builds prompt strings and response text for the sales conversation pipeline.
/// All methods are pure — no async, no side effects, no DB/AI calls.
/// </summary>
public interface ISalesPromptBuilder
{
    string BuildCustomerInstruction(VipProfile? vipProfile, bool shouldGreet, bool isReturningCustomer);
    string BuildCtaContext(StateContext ctx, CustomerIntent? intent = null);
    ResponseValidationContext BuildFactValidationContext(
        string response,
        ToneProfile? toneProfile,
        ConversationContext? conversationContext,
        SmallTalkResponse? smallTalkResponse,
        string customerMessage,
        bool requiresProductGrounding,
        IReadOnlyCollection<GroundedProduct> products,
        bool allowPolicyFacts,
        bool allowInventoryFacts,
        bool allowOrderFacts);
    string FormatAllowedProductNames(IReadOnlyCollection<GroundedProduct> products);
    string BuildPolicyGiftMessage(StateContext ctx);
    string BuildPendingContactClarificationReply(StateContext ctx);
    string BuildProductGroundingFallbackReply();
    string NormalizeSentence(string? text);
    List<string> GetMissingContactInfo(StateContext ctx);
    string BuildDraftConfirmation(StateContext ctx, DraftOrder draftOrder);
    string GetContactSummary(StateContext ctx);
    ConversationState DetermineNextState(CustomerIntent intent, bool hasProduct, bool hasContact);

    /// <summary>
    /// Returns a formatted [BỐI CẢNH PHIÊN] section when a summary exists, or empty string.
    /// Inject into system prompt between brand voice and RAG context.
    /// </summary>
    string BuildConversationSummarySection(StateContext ctx);
}
