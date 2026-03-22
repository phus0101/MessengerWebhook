using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class OrderPlacedStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.OrderPlaced;

    public OrderPlacedStateHandler(
        IGeminiService geminiService,
        ILogger<OrderPlacedStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for track order command
        if (lowerMessage.Contains("track"))
        {
            ctx.CurrentState = ConversationState.OrderTracking;
            var orderId = ctx.GetData<string>("orderId") ?? "N/A";

            var reply = $@"Order Tracking - {orderId}

Status: Processing
- Order confirmed ✓
- Payment received ✓
- Preparing shipment...

We'll notify you when your order ships!";

            AddToHistory(ctx, "model", reply);
            return reply;
        }

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

        // Default response
        var defaultReply = "Your order has been placed! Type 'track order' to check status or 'menu' for main menu.";
        AddToHistory(ctx, "model", defaultReply);
        return defaultReply;
    }
}
