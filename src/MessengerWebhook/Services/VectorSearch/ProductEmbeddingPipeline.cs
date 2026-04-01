using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace MessengerWebhook.Services.VectorSearch;

public class ProductEmbeddingPipeline
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearch;
    private readonly MessengerBotDbContext _dbContext;
    private readonly ILogger<ProductEmbeddingPipeline> _logger;

    public ProductEmbeddingPipeline(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearch,
        MessengerBotDbContext dbContext,
        ILogger<ProductEmbeddingPipeline> logger)
    {
        _embeddingService = embeddingService;
        _vectorSearch = vectorSearch;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task IndexProductAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            throw new ArgumentException($"Product {productId} not found");
        }

        var text = BuildProductText(product);
        var embedding = await _embeddingService.EmbedAsync(text, cancellationToken);

        var productEmbedding = await _dbContext.ProductEmbeddings
            .FirstOrDefaultAsync(e => e.ProductId == productId, cancellationToken);

        if (productEmbedding == null)
        {
            productEmbedding = new ProductEmbedding
            {
                Id = Guid.NewGuid(),
                TenantId = product.TenantId,
                ProductId = productId,
                Embedding = new Vector(embedding),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ProductEmbeddings.Add(productEmbedding);
        }
        else
        {
            productEmbedding.Embedding = new Vector(embedding);
            productEmbedding.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Upsert to Pinecone with graceful degradation
        try
        {
            var metadata = BuildMetadata(product);
            await _vectorSearch.UpsertProductAsync(
                productId,
                embedding,
                metadata,
                cancellationToken);

            _logger.LogInformation(
                "Indexed product {ProductId}: {Name} to both pgvector and Pinecone",
                productId,
                product.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Pinecone upsert failed for {ProductId}. pgvector succeeded.",
                productId);
            // Don't throw - dual storage strategy
        }
    }

    public async Task IndexAllProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await _dbContext.Products.ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Starting batch indexing for {Count} products",
            products.Count);

        var batchSize = 10;
        for (int i = 0; i < products.Count; i += batchSize)
        {
            var batch = products.Skip(i).Take(batchSize).ToList();

            var texts = batch.Select(BuildProductText).ToList();
            var embeddings = await _embeddingService.EmbedBatchAsync(
                texts,
                cancellationToken);

            // Save to pgvector first
            foreach (var (product, idx) in batch.Select((p, i) => (p, i)))
            {
                var productEmbedding = new ProductEmbedding
                {
                    Id = Guid.NewGuid(),
                    TenantId = product.TenantId,
                    ProductId = product.Id,
                    Embedding = new Vector(embeddings[idx]),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.ProductEmbeddings.Add(productEmbedding);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Upsert batch to Pinecone with graceful degradation
            try
            {
                var pineconeBatch = batch.Select((product, idx) => (
                    productId: product.Id,
                    embedding: embeddings[idx],
                    metadata: BuildMetadata(product)
                )).ToList();

                await _vectorSearch.UpsertBatchAsync(
                    pineconeBatch,
                    cancellationToken);

                _logger.LogInformation(
                    "Indexed batch {Current}/{Total} to both pgvector and Pinecone",
                    Math.Min(i + batchSize, products.Count),
                    products.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Pinecone batch upsert failed for batch starting at {Index}. pgvector succeeded.",
                    i);
                // Don't throw - dual storage strategy
            }
        }

        _logger.LogInformation("Batch indexing complete");
    }

    private string BuildProductText(Product product)
    {
        return $"{product.Name}. {product.Description}. " +
               $"Danh mục: {product.Category}. " +
               $"Giá: {product.BasePrice:N0}đ";
    }

    private Dictionary<string, object> BuildMetadata(Product product)
    {
        return new Dictionary<string, object>
        {
            ["product_code"] = product.Code,
            ["name"] = product.Name,
            ["category"] = product.Category.ToString(),
            ["price"] = product.BasePrice,
            ["tenant_id"] = product.TenantId?.ToString() ?? ""
        };
    }
}
