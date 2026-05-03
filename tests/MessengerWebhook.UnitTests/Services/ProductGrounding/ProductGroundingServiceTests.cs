using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.ProductGrounding;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.ProductGrounding;

public class ProductGroundingServiceTests
{
    private readonly ProductGroundingService _service = new(new ProductNeedDetector(), new ProductMentionDetector());

    [Fact]
    public void BuildContext_ProductNeedWithoutAllowedProducts_ReturnsRequiredEmptyContext()
    {
        var context = _service.BuildContext("mặt nạ dưỡng ẩm", Array.Empty<Product>(), Array.Empty<GroundedProduct>());

        Assert.True(context.RequiresGrounding);
        Assert.False(context.HasAllowedProducts);
        Assert.Equal(ProductGroundingService.FallbackReply, context.FallbackReply);
    }

    [Fact]
    public void SanitizeAssistantHistory_RemovesUnverifiedAssistantProductMention()
    {
        var history = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "mặt nạ dưỡng ẩm" },
            new() { Role = "assistant", Content = "Dạ bên em có mặt nạ Mặt nạ Tảo Biển Tươi Múi Xù ạ." },
            new() { Role = "assistant", Content = "Dạ chị cần em tư vấn thêm gì ạ?" }
        };

        var sanitized = _service.SanitizeAssistantHistory(history, Array.Empty<GroundedProduct>());

        Assert.Equal(2, sanitized.Count);
        Assert.DoesNotContain(sanitized, message => message.Content.Contains("Tảo Biển", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SanitizeAssistantHistory_RemovesLowercaseUnverifiedAssistantProductMention()
    {
        var history = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "mặt nạ dưỡng ẩm" },
            new() { Role = "assistant", Content = "Dạ bên em có tảo biển tươi múi xù cấp ẩm tốt ạ." },
            new() { Role = "assistant", Content = "Dạ chị cần em tư vấn thêm gì ạ?" }
        };

        var sanitized = _service.SanitizeAssistantHistory(history, Array.Empty<GroundedProduct>());

        Assert.Equal(2, sanitized.Count);
        Assert.DoesNotContain(sanitized, message => message.Content.Contains("tảo biển", StringComparison.OrdinalIgnoreCase));
    }
}
