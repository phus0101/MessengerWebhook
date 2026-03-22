using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class OrderTrackingStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.OrderTracking;

    public OrderTrackingStateHandler(
        IGeminiService geminiService,
        ILogger<OrderTrackingStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for menu command
        if (lowerMessage.Contains("menu") || lowerMessage.Contains("main"))
        {
            ctx.CurrentState = ConversationState.MainMenu;
            var response = @"Main Menu:
1. Browse Products
2. Skin Analysis
3. Track Order
4. Help

What would you like to do?";

            AddToHistory(ctx, "model", response);
            return response;
        }

        // Show tracking details
        var orderId = ctx.GetData<string>("orderId") ?? "N/A";

        var reply = $@"Order Tracking - {orderId}

Current Status: Processing
- Order confirmed ✓
- Payment received ✓
- Preparing shipment...

Estimated delivery: 3-5 business days

Type 'menu' to return to main menu.";

        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
