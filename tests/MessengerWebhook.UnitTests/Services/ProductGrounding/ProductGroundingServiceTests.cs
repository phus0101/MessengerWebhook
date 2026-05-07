using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Configuration;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
    public void ExtractRelatedCriteria_MaskMoisturizingNeed_ReturnsCategoryAndTerms()
    {
        var criteria = ProductGroundingService.ExtractRelatedCriteria("tôi đang tìm sản phẩm mặt nạ dưỡng ẩm");

        Assert.Equal(ProductCategory.Cosmetics, criteria.Category);
        Assert.Contains("mat na", criteria.Terms);
        Assert.Contains("dưỡng ẩm", criteria.Terms);
    }

    [Fact]
    public async Task BuildContextWithRelatedSuggestionsAsync_RelatedProducts_ReturnsGroundedReply()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(x => x.TenantId).Returns(tenantId);

        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetActiveRelatedAsync(
                tenantId,
                ProductCategory.Cosmetics,
                It.Is<IReadOnlyCollection<string>>(terms => terms.Contains("mat na")),
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                new() { Id = "p1", Code = "MN01", Name = "Mặt nạ cấp ẩm Rau Má", Category = ProductCategory.Cosmetics, BasePrice = 120000m },
                new() { Id = "p2", Code = "MN02", Name = "Mặt nạ phục hồi B5", Category = ProductCategory.Cosmetics, BasePrice = 150000m }
            });

        var service = new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector(), repository.Object, tenantContext.Object);

        var context = await service.BuildContextWithRelatedSuggestionsAsync("tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Array.Empty<Product>(), Array.Empty<GroundedProduct>());

        Assert.True(context.RequiresGrounding);
        Assert.False(context.HasAllowedProducts);
        Assert.True(context.HasRelatedSuggestions);
        Assert.Equal(2, context.RelatedSuggestions.Count);
        Assert.Contains("Mặt nạ cấp ẩm Rau Má", context.RelatedSuggestionReply);
        Assert.Contains("MN01", context.RelatedSuggestionReply);
        Assert.Contains("120,000đ", context.RelatedSuggestionReply);
    }

    [Fact]
    public async Task BuildContextWithRelatedSuggestionsAsync_RelatedReply_PassesRealGroundingValidation()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(x => x.TenantId).Returns(tenantId);

        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetActiveRelatedAsync(
                tenantId,
                ProductCategory.Cosmetics,
                It.IsAny<IReadOnlyCollection<string>>(),
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                new() { Id = "p1", Code = "MN01", Name = "Mặt nạ cấp ẩm Rau Má", Category = ProductCategory.Cosmetics, BasePrice = 120000m }
            });

        var service = new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector(), repository.Object, tenantContext.Object);
        var context = await service.BuildContextWithRelatedSuggestionsAsync("tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Array.Empty<Product>(), Array.Empty<GroundedProduct>());
        var validator = new ResponseValidationService(
            Options.Create(new ResponseValidationOptions
            {
                EnableValidation = true,
                EnableToneValidation = false,
                EnableContextValidation = false,
                EnableLanguageValidation = false,
                EnableStructureValidation = false,
                BlockOnErrors = true
            }),
            NullLogger<ResponseValidationService>.Instance);

        var result = await validator.ValidateAsync(new ResponseValidationContext
        {
            Response = context.RelatedSuggestionReply!,
            RequiresFactGrounding = true,
            AllowedProductNames = context.RelatedSuggestions.Select(product => product.Name).ToList(),
            AllowedProductCodes = context.RelatedSuggestions.Select(product => product.Code).ToList(),
            AllowedPrices = context.RelatedSuggestions.Where(product => product.Price.HasValue).Select(product => product.Price!.Value).ToList(),
            AllowPolicyFacts = false,
            AllowInventoryFacts = false,
            AllowOrderFacts = false
        });

        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public async Task BuildContextWithRelatedSuggestionsAsync_NoRelatedProducts_ReturnsFallbackOnly()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(x => x.TenantId).Returns(tenantId);

        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetActiveRelatedAsync(It.IsAny<Guid>(), It.IsAny<ProductCategory?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());

        var service = new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector(), repository.Object, tenantContext.Object);

        var context = await service.BuildContextWithRelatedSuggestionsAsync("tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Array.Empty<Product>(), Array.Empty<GroundedProduct>());

        Assert.True(context.RequiresGrounding);
        Assert.False(context.HasRelatedSuggestions);
        Assert.Null(context.RelatedSuggestionReply);
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

    // Regression for normalizer mismatch (C4): an allowed product with diacritics must match
    // a no-diacritic mention identically across sanitizer and validator.
    [Fact]
    public void SanitizeAssistantHistory_AllowedProductDiacritics_MentionWithoutDiacritics_KeepsMessage()
    {
        var allowed = new[] { new GroundedProduct("p1", "MN01", "Mặt nạ cấp ẩm Rau Má", "Cosmetics", 120000m) };
        var history = new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Da ben em co mat na cap am rau ma rat tot a." }
        };

        var sanitized = _service.SanitizeAssistantHistory(history, allowed);

        Assert.Single(sanitized);
    }

    [Fact]
    public void SanitizeAssistantHistory_AllowedProductWithoutDiacritics_MentionWithDiacritics_KeepsMessage()
    {
        var allowed = new[] { new GroundedProduct("p1", "MN01", "Mat na cap am Rau Ma", "Cosmetics", 120000m) };
        var history = new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Dạ bên em có Mặt nạ cấp ẩm Rau Má rất tốt ạ." }
        };

        var sanitized = _service.SanitizeAssistantHistory(history, allowed);

        Assert.Single(sanitized);
    }
}
