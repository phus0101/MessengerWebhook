using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MessengerWebhook.Data.Repositories;

public class VectorSearchRepository : IVectorSearchRepository
{
    private readonly MessengerBotDbContext _context;
    private readonly ILogger<VectorSearchRepository> _logger;

    public VectorSearchRepository(
        MessengerBotDbContext context,
        ILogger<VectorSearchRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Product>> SearchSimilarProductsAsync(
        float[] queryEmbedding,
        int limit = 5,
        double similarityThreshold = 0.7,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding, nameof(queryEmbedding));

        if (queryEmbedding.Length != 768)
        {
            throw new ArgumentException("Embedding must have 768 dimensions", nameof(queryEmbedding));
        }

        // Use raw SQL for pgvector cosine similarity search
        var sql = @"
            SELECT ""Id"", ""Name"", ""Description"", ""Brand"", ""Category"", ""BasePrice"",
                   ""IngredientsJson"", ""SkinTypesJson"", ""SkinConcernsJson"", ""pH"", ""Texture"",
                   ""ContraindicationsJson"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"",
                   1 - (""Embedding"" <=> @embedding::vector) AS similarity
            FROM ""Products""
            WHERE ""Embedding"" IS NOT NULL
              AND ""IsActive"" = true
              AND 1 - (""Embedding"" <=> @embedding::vector) >= @threshold
            ORDER BY ""Embedding"" <=> @embedding::vector
            LIMIT @limit";

        var embeddingParam = new NpgsqlParameter("@embedding", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Real)
        {
            Value = queryEmbedding
        };

        var products = await _context.Products
            .FromSqlRaw(sql,
                embeddingParam,
                new NpgsqlParameter("@threshold", similarityThreshold),
                new NpgsqlParameter("@limit", limit))
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Vector search returned {Count} products with similarity >= {Threshold}",
            products.Count, similarityThreshold);

        return products;
    }

    public async Task UpdateProductEmbeddingAsync(
        string productId,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId, nameof(productId));
        ArgumentNullException.ThrowIfNull(embedding, nameof(embedding));

        if (embedding.Length != 768)
        {
            throw new ArgumentException("Embedding must have 768 dimensions", nameof(embedding));
        }

        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID {productId} not found");
        }

        product.Embedding = embedding;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated embedding for product {ProductId}", productId);
    }
}
