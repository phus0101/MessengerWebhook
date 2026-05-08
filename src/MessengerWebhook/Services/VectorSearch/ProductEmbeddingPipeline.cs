using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace MessengerWebhook.Services.VectorSearch;

public class ProductEmbeddingPipeline
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearch;
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProductEmbeddingPipeline> _logger;
    private readonly IIndexingProgressTracker? _progressTracker;

    public ProductEmbeddingPipeline(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearch,
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<ProductEmbeddingPipeline> logger,
        IIndexingProgressTracker? progressTracker = null)
    {
        _embeddingService = embeddingService;
        _vectorSearch = vectorSearch;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
        _progressTracker = progressTracker;
    }

    public async Task IndexProductAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant context is required to index products");
        }

        var tenantId = _tenantContext.TenantId.Value;
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && p.TenantId == tenantId, cancellationToken);

        if (product == null)
        {
            throw new ArgumentException($"Product {productId} not found");
        }

        var text = BuildProductText(product);
        var embedding = await _embeddingService.EmbedAsync(text, cancellationToken);

        if (SupportsProductEmbeddings())
        {
            await UpsertProductEmbeddingAsync(tenantId, productId, embedding, cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Skipping pgvector persistence for product {ProductId} because ProductEmbedding is not in the current EF model",
                productId);
        }

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
        Guid? jobId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant context is required to index products");
        }

        var tenantId = _tenantContext.TenantId.Value;
        var products = await _dbContext.Products
            .Where(p => p.IsActive && p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Starting batch indexing for {Count} products",
            products.Count);

        var batchSize = 10;
        var indexedCount = 0;

        try
        {
            for (int i = 0; i < products.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = products.Skip(i).Take(batchSize).ToList();

                var texts = batch.Select(BuildProductText).ToList();
                var embeddings = await _embeddingService.EmbedBatchAsync(
                    texts,
                    cancellationToken);

                if (SupportsProductEmbeddings())
                {
                    foreach (var (product, idx) in batch.Select((p, i) => (p, i)))
                    {
                        await UpsertProductEmbeddingAsync(tenantId, product.Id, embeddings[idx], cancellationToken);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Skipping pgvector persistence for batch starting at index {Index} because ProductEmbedding is not in the current EF model",
                        i);
                }

                indexedCount += batch.Count;

                // Report progress based on local batch processing, even if Pinecone degrades.
                if (jobId.HasValue && _progressTracker != null)
                {
                    var currentProduct = batch.Last();
                    _progressTracker.UpdateProgress(
                        jobId.Value,
                        indexedCount,
                        currentProduct.Id,
                        currentProduct.Name);
                }

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
                        "Pinecone batch upsert failed for batch starting at {Index}. Local indexing still succeeded.",
                        i);
                }
            }

            // Mark job as completed
            if (jobId.HasValue && _progressTracker != null)
            {
                _progressTracker.CompleteJob(jobId.Value);
            }

            _logger.LogInformation("Batch indexing complete");
        }
        catch (Exception ex)
        {
            // Mark job as failed
            if (jobId.HasValue && _progressTracker != null)
            {
                _progressTracker.FailJob(jobId.Value, ex.Message);
            }
            throw;
        }
    }

    private async Task UpsertProductEmbeddingAsync(
        Guid tenantId,
        string productId,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sql = @"
            INSERT INTO ""ProductEmbeddings"" (""Id"", ""TenantId"", ""ProductId"", ""Embedding"", ""CreatedAt"", ""UpdatedAt"")
            VALUES (@id, @tenantId, @productId, @embedding::vector, @createdAt, @updatedAt)
            ON CONFLICT (""TenantId"", ""ProductId"") DO UPDATE SET
                ""Embedding"" = EXCLUDED.""Embedding"",
                ""UpdatedAt"" = EXCLUDED.""UpdatedAt"";";

        var embeddingParam = new NpgsqlParameter("@embedding", NpgsqlDbType.Array | NpgsqlDbType.Real)
        {
            Value = embedding
        };

        await _dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new object[]
            {
                new NpgsqlParameter("@id", Guid.NewGuid()),
                new NpgsqlParameter("@tenantId", tenantId),
                new NpgsqlParameter("@productId", productId),
                embeddingParam,
                new NpgsqlParameter("@createdAt", now),
                new NpgsqlParameter("@updatedAt", now)
            },
            cancellationToken);
    }

    private bool SupportsProductEmbeddings()
    {
        return _dbContext.Model.FindEntityType(typeof(ProductEmbedding)) != null;
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
            ["product_id"] = product.Id,
            ["product_code"] = product.Code,
            ["name"] = product.Name,
            ["category"] = product.Category.ToString(),
            ["price"] = product.BasePrice,
            ["tenant_id"] = product.TenantId?.ToString() ?? "",
            ["is_active"] = product.IsActive
        };
    }
}
