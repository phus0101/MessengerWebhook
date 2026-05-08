using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class SkinConsultationStateHandler : BaseStateHandler
{
    private readonly IVectorSearchRepository _vectorSearchRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITenantContext _tenantContext;

    public override ConversationState HandledState => ConversationState.SkinConsultation;

    public SkinConsultationStateHandler(
        IGeminiService geminiService,
        IVectorSearchRepository vectorSearchRepository,
        IEmbeddingService embeddingService,
        ITenantContext tenantContext,
        ILogger<SkinConsultationStateHandler> logger)
        : base(geminiService, logger)
    {
        _vectorSearchRepository = vectorSearchRepository;
        _embeddingService = embeddingService;
        _tenantContext = tenantContext;
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

        if (!_tenantContext.TenantId.HasValue)
        {
            return "Em chưa xác định được catalog của shop. Chị thử tìm lại sản phẩm giúp em nhé.";
        }

        var embedding = await _embeddingService.EmbedAsync(message);
        var products = await _vectorSearchRepository.SearchSimilarProductsAsync(embedding, _tenantContext.TenantId.Value, limit: 3);

        var reply = "Dạ em ghi nhận nhu cầu chăm sóc da của chị. Em chỉ gợi ý sản phẩm đang có trong catalog của shop bên dưới ạ.";

        if (products.Count > 0)
        {
            var productList = string.Join("\n", products.Select((p, i) =>
                $"{i + 1}. {p.Name} - {p.BasePrice:N0}đ"));

            reply += $"\n\nSản phẩm phù hợp trong catalog:\n{productList}\n\nChị trả lời số để xem chi tiết hoặc gõ 'menu' để về menu chính.";
            ctx.SetData("searchResults", products.Select(p => p.Id).ToList());
            ctx.CurrentState = ConversationState.BrowsingProducts;
        }
        else
        {
            reply += "\n\nChị gõ 'menu' để về menu chính.";
        }

        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
