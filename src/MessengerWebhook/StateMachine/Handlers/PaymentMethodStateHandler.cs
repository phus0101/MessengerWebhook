using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class PaymentMethodStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.PaymentMethod;

    public PaymentMethodStateHandler(
        IGeminiService geminiService,
        ILogger<PaymentMethodStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        // Check for back command
        if (message.ToLowerInvariant().Contains("back") || message.ToLowerInvariant().Contains("address"))
        {
            ctx.CurrentState = ConversationState.ShippingAddress;
            return "Returning to shipping address.";
        }

        // Parse payment method selection
        var prompt = $@"User said: '{message}'
Extract payment method: credit card, paypal, or cash on delivery.
Respond with ONLY the payment method name.";

        var history = GetHistory(ctx);
        var paymentMethod = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        paymentMethod = paymentMethod.Trim().ToLowerInvariant();

        ctx.SetData("paymentMethod", paymentMethod);
        Logger.LogInformation("Payment method selected: {PaymentMethod} for PSID: {PSID}", paymentMethod, ctx.FacebookPSID);

        ctx.CurrentState = ConversationState.OrderConfirmation;

        var shippingAddress = ctx.GetData<string>("shippingAddress") ?? "N/A";
        var cartItems = ctx.GetData<List<string>>("cartItems") ?? new List<string>();

        var reply = $@"Order Summary:
- Items: {cartItems.Count} product(s)
- Shipping: {shippingAddress}
- Payment: {paymentMethod}

Type 'confirm' to place order or 'back' to modify.";

        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
