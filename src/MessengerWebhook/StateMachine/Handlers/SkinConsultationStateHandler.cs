using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class SkinConsultationStateHandler : BaseStateHandler
{
    private readonly IVectorSearchRepository _vectorSearchRepository;
    private readonly IEmbeddingService _embeddingService;

    public override ConversationState HandledState => ConversationState.SkinConsultation;

    public SkinConsultationStateHandler(
        IGeminiService geminiService,
        IVectorSearchRepository vectorSearchRepository,
        IEmbeddingService embeddingService,
        ILogger<SkinConsultationStateHandler> logger)
        : base(geminiService, logger)
    {
        _vectorSearchRepository = vectorSearchRepository;
        _embeddingService = embeddingService;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for menu command
        if (lowerMessage.Contains("menu") || lowerMessage.Contains("main"))
        {
            ctx.CurrentState = ConversationState.MainMenu;
            var menuResponse = @"Main Menu:
1. Browse Products
2. Skin Analysis
3. Track Order
4. Help

What would you like to do?";

            AddToHistory(ctx, "model", menuResponse);
            return menuResponse;
        }

        // Use Gemini to provide personalized skin consultation
        var prompt = $@"User's skin concern: '{message}'

Provide a brief, helpful response about their skin concern.
Include general advice and suggest they can browse products for their skin type.
Keep response under 100 words.";

        var history = GetHistory(ctx);
        var consultation = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

        // Search for relevant products
        var embedding = await _embeddingService.EmbedAsync(message);
        var products = await _vectorSearchRepository.SearchSimilarProductsAsync(embedding, limit: 3);

        var reply = consultation;

        if (products.Count > 0)
        {
            var productList = string.Join("\n", products.Select((p, i) =>
                $"{i + 1}. {p.Name} - ${p.BasePrice:F2}"));

            reply += $"\n\nRecommended products:\n{productList}\n\nType a number to see details or 'menu' for main menu.";
            ctx.SetData("searchResults", products.Select(p => p.Id).ToList());
            ctx.CurrentState = ConversationState.BrowsingProducts;
        }
        else
        {
            reply += "\n\nType 'menu' to return to main menu.";
        }

        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
