using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.Sales.Contact;

/// <summary>
/// Encapsulates all contact confirmation decision logic extracted from SalesStateHandlerBase.
/// Pure reads + in-memory StateContext mutations — no DB writes, no Messenger sends.
///
/// Truth table (contactNeedsConfirmation=C, pendingContactQuestion="confirm_old_contact"=P):
///   C=T, P=T, message=clarification     → IsPendingClarificationQuestion → send clarification reply
///   C=T, P=T, message=generic buy       → IsGenericBuyContinuationPendingConfirmation → send confirmation request
///   C=T, P=T, message=confirms old      → SalesMessageParser sets C=F; handled by base class
///   C=F, all info present               → HasRequiredContact → ready for order
///   C=F, missing info                   → BuildContactCollectionReplyAsync → ask for missing field(s)
/// </summary>
public interface IContactConfirmationFlow
{
    /// <summary>Returns true if message is asking "do you have my contact info?" (any form).</summary>
    bool IsContactMemoryQuestion(string message);

    /// <summary>
    /// Returns true when contact confirmation is pending (contactNeedsConfirmation=true,
    /// pendingContactQuestion="confirm_old_contact") AND the message is asking which info to confirm.
    /// </summary>
    bool IsPendingClarificationQuestion(StateContext ctx, string message);

    /// <summary>
    /// Returns true when contact confirmation is pending AND the message is a generic buy signal
    /// (ok, chốt, lên đơn, etc.) rather than an explicit confirmation of the remembered contact.
    /// </summary>
    bool IsGenericBuyContinuationPendingConfirmation(StateContext ctx, string message);

    /// <summary>
    /// Builds the reply for "do you have my contact info?" queries.
    /// Varies based on what info is on file and whether confirmation is still pending.
    /// </summary>
    Task<string?> BuildContactMemoryReplyAsync(StateContext ctx, string message);

    /// <summary>
    /// Builds the reply prompting the customer to confirm existing contact or provide missing fields.
    /// Returns null if all contact info is already present and confirmed (no reply needed).
    /// </summary>
    Task<string?> BuildContactCollectionReplyAsync(StateContext ctx, string message);
}
