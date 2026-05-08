using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

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
        Guid tenantId,
        int limit = 5,
        double similarityThreshold = 0.7,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding, nameof(queryEmbedding));

        if (queryEmbedding.Length != 768)
        {
            throw new ArgumentException("Embedding must have 768 dimensions", nameof(queryEmbedding));
        }

        // Use raw SQL for pgvector cosine similarity search against ProductEmbeddings
        var sql = @"
            SELECT p.*
            FROM ""Products"" p
            INNER JOIN ""ProductEmbeddings"" pe ON p.""Id"" = pe.""ProductId""
            WHERE p.""IsActive"" = true
              AND p.""TenantId"" = @tenantId
              AND pe.""TenantId"" = @tenantId
              AND 1 - (pe.""Embedding"" <=> @embedding::vector) >= @threshold
            ORDER BY pe.""Embedding"" <=> @embedding::vector
            LIMIT @limit";

        var embeddingParam = new NpgsqlParameter("@embedding", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Real)
        {
            Value = queryEmbedding
        };

        var products = await _context.Products
            .FromSqlRaw(sql,
                embeddingParam,
                new NpgsqlParameter("@tenantId", tenantId),
                new NpgsqlParameter("@threshold", similarityThreshold),
                new NpgsqlParameter("@limit", limit))
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Vector search returned {Count} products for tenant {TenantId} with similarity >= {Threshold}",
            products.Count, tenantId, similarityThreshold);

        return products;
    }

    public async Task UpdateProductEmbeddingAsync(
        string productId,
        Guid tenantId,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId, nameof(productId));
        ArgumentNullException.ThrowIfNull(embedding, nameof(embedding));

        if (embedding.Length != 768)
        {
            throw new ArgumentException("Embedding must have 768 dimensions", nameof(embedding));
        }

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == productId && p.IsActive && p.TenantId == tenantId)
            .Select(p => new { p.Id, p.TenantId })
            .SingleOrDefaultAsync(cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Active tenant product with ID {productId} not found");
        }

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

        await _context.Database.ExecuteSqlRawAsync(
            sql,
            new object[]
            {
                new NpgsqlParameter("@id", Guid.NewGuid()),
                new NpgsqlParameter("@tenantId", tenantId),
                new NpgsqlParameter("@productId", product.Id),
                embeddingParam,
                new NpgsqlParameter("@createdAt", now),
                new NpgsqlParameter("@updatedAt", now)
            },
            cancellationToken);

        _logger.LogDebug("Updated embedding for product {ProductId}", productId);
    }
}
