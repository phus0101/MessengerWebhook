using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class BrowsingProductsStateHandler : BaseStateHandler
{
    private readonly IVectorSearchRepository _vectorSearchRepository;
    private readonly IEmbeddingService _embeddingService;

    public override ConversationState HandledState => ConversationState.BrowsingProducts;

    public BrowsingProductsStateHandler(
        IGeminiService geminiService,
        
        IVectorSearchRepository vectorSearchRepository,
        IEmbeddingService embeddingService,
        ILogger<BrowsingProductsStateHandler> logger)
        : base(geminiService, logger)
    {
        _vectorSearchRepository = vectorSearchRepository;
        _embeddingService = embeddingService;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        // Check for navigation commands
        if (message.ToLowerInvariant().Contains("cart"))
        {
            var cartItems = ctx.GetData<List<string>>("cartItems");
            if (cartItems?.Count > 0)
            {
                ctx.CurrentState = ConversationState.CartReview;
                return "Let me show you your cart.";
            }
            return "Your cart is empty. Continue browsing to add products!";
        }

        if (message.ToLowerInvariant().Contains("menu") || message.ToLowerInvariant().Contains("back"))
        {
            ctx.CurrentState = ConversationState.MainMenu;
            return "Returning to main menu.";
        }

        // Generate embedding for search query
        var embedding = await _embeddingService.GenerateAsync(message);
        var products = await _vectorSearchRepository.SearchSimilarProductsAsync(embedding, limit: 5);

        if (products.Count == 0)
        {
            var response = "I couldn't find products matching your search. Try different keywords like 'moisturizer', 'serum', or 'cleanser'.";
            AddToHistory(ctx, "model", response);
            return response;
        }

        ctx.SetData("searchResults", products.Select(p => p.Id).ToList());

        var productList = string.Join("\n", products.Select((p, i) =>
            $"{i + 1}. {p.Name} by {p.Brand} - ${p.BasePrice:F2}\n   {p.Description.Substring(0, Math.Min(80, p.Description.Length))}..."));

        var reply = $"Found {products.Count} products:\n\n{productList}\n\nReply with a number to see details, or describe what you're looking for.";
        AddToHistory(ctx, "model", reply);

        return reply;
    }
}
