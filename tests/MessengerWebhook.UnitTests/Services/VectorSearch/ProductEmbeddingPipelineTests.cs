using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.VectorSearch;

public class ProductEmbeddingPipelineTests
{
    [Fact]
    public async Task IndexProductAsync_ShouldIndexOnlyActiveProductForResolvedTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);
        await using var dbContext = CreateDbContext(tenantContext);
        dbContext.Products.AddRange(
            CreateProduct("active-current", tenantId, true),
            CreateProduct("active-other", otherTenantId, true));
        await dbContext.SaveChangesAsync();

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Repeat(0.1f, 768).ToArray());

        var vectorSearch = new Mock<IVectorSearchService>();
        vectorSearch
            .Setup(x => x.UpsertProductAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpsertResult { UpsertedCount = 1 });

        var pipeline = new ProductEmbeddingPipeline(
            embeddingService.Object,
            vectorSearch.Object,
            dbContext,
            tenantContext,
            NullLogger<ProductEmbeddingPipeline>.Instance);

        await pipeline.IndexProductAsync("active-current");
        var otherTenantAct = async () => await pipeline.IndexProductAsync("active-other");

        await Assert.ThrowsAsync<ArgumentException>(otherTenantAct);
        vectorSearch.Verify(x => x.UpsertProductAsync(
            "active-current",
            It.IsAny<float[]>(),
            It.Is<Dictionary<string, object>>(metadata =>
                metadata["product_id"].Equals("active-current") &&
                metadata["tenant_id"].Equals(tenantId.ToString()) &&
                metadata["is_active"].Equals(true)),
            It.IsAny<CancellationToken>()), Times.Once);
        vectorSearch.Verify(x => x.UpsertProductAsync(
            "active-other",
            It.IsAny<float[]>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IndexAllProductsAsync_ShouldIndexOnlyActiveProductsForResolvedTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);
        await using var dbContext = CreateDbContext(tenantContext);
        dbContext.Products.AddRange(
            CreateProduct("active-current", tenantId, true),
            CreateProduct("inactive-current", tenantId, false),
            CreateProduct("active-other", otherTenantId, true),
            CreateProduct("active-null-tenant", null, true));
        await dbContext.SaveChangesAsync();

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(x => x.EmbedBatchAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) => texts.Select(_ => Enumerable.Repeat(0.1f, 768).ToArray()).ToList());

        var vectorSearch = new Mock<IVectorSearchService>();
        vectorSearch
            .Setup(x => x.UpsertBatchAsync(
                It.IsAny<List<(string productId, float[] embedding, Dictionary<string, object> metadata)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpsertResult { UpsertedCount = 1 });

        var pipeline = new ProductEmbeddingPipeline(
            embeddingService.Object,
            vectorSearch.Object,
            dbContext,
            tenantContext,
            NullLogger<ProductEmbeddingPipeline>.Instance);

        await pipeline.IndexAllProductsAsync();

        vectorSearch.Verify(x => x.UpsertBatchAsync(
            It.Is<List<(string productId, float[] embedding, Dictionary<string, object> metadata)>>(batch =>
                batch.Count == 1 &&
                batch[0].productId == "active-current" &&
                batch[0].metadata["product_id"].Equals("active-current") &&
                batch[0].metadata["tenant_id"].Equals(tenantId.ToString()) &&
                batch[0].metadata["is_active"].Equals(true)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndexAllProductsAsync_ShouldRequireResolvedTenant()
    {
        var tenantContext = new NullTenantContext();
        await using var dbContext = CreateDbContext(tenantContext);
        dbContext.Products.Add(CreateProduct("active-unscoped", Guid.NewGuid(), true));
        await dbContext.SaveChangesAsync();

        var pipeline = new ProductEmbeddingPipeline(
            Mock.Of<IEmbeddingService>(),
            Mock.Of<IVectorSearchService>(),
            dbContext,
            tenantContext,
            NullLogger<ProductEmbeddingPipeline>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.IndexAllProductsAsync());
    }

    private static MessengerBotDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MessengerBotDbContext(options, tenantContext);
    }

    private static Product CreateProduct(string id, Guid? tenantId, bool isActive)
    {
        return new Product
        {
            Id = id,
            TenantId = tenantId,
            Code = id.ToUpperInvariant(),
            Name = id,
            Description = "Test product",
            Brand = "Test",
            Category = ProductCategory.Cosmetics,
            BasePrice = 100000m,
            IsActive = isActive
        };
    }
}
