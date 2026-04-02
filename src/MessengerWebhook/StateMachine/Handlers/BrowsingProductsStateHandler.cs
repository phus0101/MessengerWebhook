using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
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
        if (message.ToLowerInvariant().Contains("cart") || message.ToLowerInvariant().Contains("giỏ"))
        {
            var cartItems = ctx.GetData<List<string>>("cartItems");
            if (cartItems?.Count > 0)
            {
                ctx.CurrentState = ConversationState.CartReview;
                return "Để tôi xem giỏ hàng của bạn.";
            }
            return "Giỏ hàng của bạn đang trống. Tiếp tục xem sản phẩm để thêm vào giỏ nhé!";
        }

        if (message.ToLowerInvariant().Contains("menu") || message.ToLowerInvariant().Contains("back") || message.ToLowerInvariant().Contains("quay"))
        {
            ctx.CurrentState = ConversationState.MainMenu;
            return "Quay lại menu chính.";
        }

        // Generate embedding for search query
        var embedding = await _embeddingService.EmbedAsync(message);
        var products = await _vectorSearchRepository.SearchSimilarProductsAsync(embedding, limit: 5);

        if (products.Count == 0)
        {
            var response = "Tôi không tìm thấy sản phẩm phù hợp. Thử từ khóa khác như 'kem dưỡng ẩm', 'serum', hoặc 'sữa rửa mặt' nhé.";
            AddToHistory(ctx, "model", response);
            return response;
        }

        ctx.SetData("searchResults", products.Select(p => p.Id).ToList());

        var productList = string.Join("\n", products.Select((p, i) =>
            $"{i + 1}. {p.Name} - {p.Brand} - {p.BasePrice:N0}đ\n   {p.Description.Substring(0, Math.Min(80, p.Description.Length))}..."));

        var reply = $"Tìm thấy {products.Count} sản phẩm:\n\n{productList}\n\nTrả lời số để xem chi tiết, hoặc mô tả bạn đang tìm gì.";
        AddToHistory(ctx, "model", reply);

        return reply;
    }
}
