using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.Sales.Reply;

public interface ISalesConsultationReplies
{
    Task<string?> TryBuildOfferResponseAsync(StateContext ctx, string message, CustomerIntent intent);
    Task<string?> BuildProductConsultationReplyAsync(StateContext ctx, string message);
    Task<string?> BuildShippingConsultationReplyAsync(StateContext ctx, string message);
    Task<string?> BuildOrderEstimateReplyAsync(StateContext ctx, string message);
    Task<string> BuildFirstGreetingReplyAsync(StateContext ctx);
    Task<string?> BuildPriceConsultationReplyAsync(StateContext ctx, string message);
    Task<string?> BuildInventoryConsultationReplyAsync(StateContext ctx, string message);
    Task<string?> BuildAmbiguousProductClarificationReplyAsync(StateContext ctx);
    Task<string?> BuildFinalOrderConfirmationReplyAsync(StateContext ctx, string message, bool forceResend = false);
}
