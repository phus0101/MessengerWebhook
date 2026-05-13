using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class SkinAnalysisStateHandler : BaseStateHandler
{
    private readonly IVectorSearchRepository _vectorSearchRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITenantContext _tenantContext;

    public override ConversationState HandledState => ConversationState.SkinAnalysis;

    public SkinAnalysisStateHandler(
        IGeminiService geminiService,
        
        IVectorSearchRepository vectorSearchRepository,
        IEmbeddingService embeddingService,
        ITenantContext tenantContext,
        ILogger<SkinAnalysisStateHandler> logger)
        : base(geminiService, logger)
    {
        _vectorSearchRepository = vectorSearchRepository;
        _embeddingService = embeddingService;
        _tenantContext = tenantContext;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var prompt = $@"User said: '{message}'
Extract skin type: oily, dry, combination, sensitive, or normal.
Respond with ONLY the skin type.";

        var history = GetHistory(ctx);
        var detectedSkinType = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        var skinType = NormalizeSkinType(detectedSkinType);

        if (skinType == null)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Dạ em cần kiểm tra thêm loại da của chị trước khi gợi ý chính xác. Chị có thể mô tả da dầu, da khô, da hỗn hợp, da nhạy cảm hay da thường không ạ?";
        }

        ctx.SetData("skinType", skinType);

        Logger.LogInformation("Detected skin type: {SkinType}", skinType);

        if (!_tenantContext.TenantId.HasValue)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Em chưa xác định được catalog của shop. Chị thử tìm lại sản phẩm giúp em nhé.";
        }

        var searchQuery = $"products for {skinType} skin";
        var embedding = await _embeddingService.EmbedAsync(searchQuery);
        var products = await _vectorSearchRepository.SearchSimilarProductsAsync(embedding, _tenantContext.TenantId.Value, limit: 5);

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

    private static string? NormalizeSkinType(string value)
    {
        var skinType = value.Trim().ToLowerInvariant();
        return skinType is "oily" or "dry" or "combination" or "sensitive" or "normal"
            ? skinType
            : null;
    }
}
