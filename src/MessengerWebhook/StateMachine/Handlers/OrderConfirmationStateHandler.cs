using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class OrderConfirmationStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.OrderConfirmation;

    public OrderConfirmationStateHandler(
        IGeminiService geminiService,
        ILogger<OrderConfirmationStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for back command
        if (lowerMessage.Contains("back") || lowerMessage.Contains("modify"))
        {
            ctx.CurrentState = ConversationState.PaymentMethod;
            return "Returning to payment method selection.";
        }

        // Check for confirmation
        if (lowerMessage.Contains("confirm") || lowerMessage.Contains("yes") || lowerMessage.Contains("place"))
        {
            // Generate order ID
            var orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            ctx.SetData("orderId", orderId);

            Logger.LogInformation("Order placed: {OrderId} for PSID: {PSID}", orderId, ctx.FacebookPSID);

            ctx.CurrentState = ConversationState.OrderPlaced;

            var reply = $@"✅ Order Placed Successfully!

Order ID: {orderId}
Estimated delivery: 3-5 business days

You can track your order anytime by typing 'track order'.
Type 'menu' to return to main menu.";

            AddToHistory(ctx, "model", reply);
            return reply;
        }

        var response = "Please type 'confirm' to place your order or 'back' to modify.";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
