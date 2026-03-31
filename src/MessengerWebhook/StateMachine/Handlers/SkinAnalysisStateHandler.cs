using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class SkinAnalysisStateHandler : BaseStateHandler
{
    private readonly IVectorSearchRepository _vectorSearchRepository;
    private readonly IEmbeddingService _embeddingService;

    public override ConversationState HandledState => ConversationState.SkinAnalysis;

    public SkinAnalysisStateHandler(
        IGeminiService geminiService,
        
        IVectorSearchRepository vectorSearchRepository,
        IEmbeddingService embeddingService,
        ILogger<SkinAnalysisStateHandler> logger)
        : base(geminiService, logger)
    {
        _vectorSearchRepository = vectorSearchRepository;
        _embeddingService = embeddingService;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var prompt = $@"User said: '{message}'
Extract skin type: oily, dry, combination, sensitive, or normal.
Respond with ONLY the skin type.";

        var history = GetHistory(ctx);
        var skinType = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        skinType = skinType.Trim().ToLowerInvariant();

        ctx.SetData("skinType", skinType);

        Logger.LogInformation("Detected skin type: {SkinType} for PSID: {PSID}", skinType, ctx.FacebookPSID);

        // Search for products suitable for this skin type
        var searchQuery = $"products for {skinType} skin";
        var embedding = await _embeddingService.GenerateAsync(searchQuery);
        var products = await _vectorSearchRepository.SearchSimilarProductsAsync(embedding, limit: 5);

        ctx.CurrentState = ConversationState.BrowsingProducts;

        if (products.Count == 0)
        {
            var response = $"Cảm ơn! Tôi đã ghi nhận loại da của bạn là {skinType}. Để tôi tìm sản phẩm phù hợp.";
            AddToHistory(ctx, "model", response);
            return response;
        }

        ctx.SetData("searchResults", products.Select(p => p.Id).ToList());

        var productList = string.Join("\n", products.Select((p, i) =>
            $"{i + 1}. {p.Name} - {p.Brand} - {p.BasePrice:N0}đ"));

        var reply = $"Hoàn hảo! Cho da {skinType}, tôi gợi ý:\n\n{productList}\n\nTrả lời số để xem chi tiết.";
        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
