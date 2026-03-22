using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class ShippingAddressStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.ShippingAddress;

    public ShippingAddressStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger<ShippingAddressStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        // Check for back command
        if (message.ToLowerInvariant().Contains("back") || message.ToLowerInvariant().Contains("cart"))
        {
            await TransitionToAsync(ctx, ConversationState.CartReview);
            return "Returning to cart review.";
        }

        // Use Gemini to validate and parse address
        var prompt = $@"User provided address: '{message}'
Validate if this looks like a valid shipping address (street, city, postal code).
Respond with 'valid' or 'invalid'.";

        var history = GetHistory(ctx);
        var validation = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        validation = validation.Trim().ToLowerInvariant();

        if (validation.Contains("invalid"))
        {
            var response = "Please provide a complete address including street, city, and postal code.";
            AddToHistory(ctx, "model", response);
            return response;
        }

        ctx.SetData("shippingAddress", message);
        Logger.LogInformation("Shipping address saved for PSID: {PSID}", ctx.FacebookPSID);

        await TransitionToAsync(ctx, ConversationState.PaymentMethod);

        var reply = "Address saved! Now, please select payment method:\n1. Credit Card\n2. PayPal\n3. Cash on Delivery";
        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
