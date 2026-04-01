using MessengerWebhook.Configuration;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Options;
using Pinecone;

namespace MessengerWebhook.Services.VectorSearch;

public class PineconeVectorService : IVectorSearchService
{
    private readonly PineconeClient _client;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PineconeVectorService> _logger;
    private readonly PineconeOptions _options;

    public PineconeVectorService(
        PineconeClient client,
        IOptions<PineconeOptions> options,
        ITenantContext tenantContext,
        ILogger<PineconeVectorService> logger)
    {
        _client = client;
        _tenantContext = tenantContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<UpsertResult> UpsertProductAsync(
        string productId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantNamespace();
        var vectorId = $"{tenantId}-{productId}";
        var index = _client.Index(_options.IndexName);

        var vector = new Vector
        {
            Id = vectorId,
            Values = embedding,
            Metadata = ConvertToMetadata(metadata)
        };

        try
        {
            var response = await index.UpsertAsync(
                new UpsertRequest
                {
                    Vectors = new[] { vector },
                    Namespace = tenantId
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Upserted product {ProductId} to Pinecone namespace {Namespace}",
                productId, tenantId);

            return new UpsertResult
            {
                UpsertedCount = (int)response.UpsertedCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upsert product {ProductId} to Pinecone",
                productId);
            throw;
        }
    }

    public async Task<UpsertResult> UpsertBatchAsync(
        List<(string productId, float[] embedding, Dictionary<string, object> metadata)> products,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantNamespace();
        var index = _client.Index(_options.IndexName);
        var totalUpserted = 0;
        var failedIds = new List<string>();

        // Split into chunks of 100 (Pinecone limit)
        const int batchSize = 100;
        for (int i = 0; i < products.Count; i += batchSize)
        {
            var batch = products.Skip(i).Take(batchSize).ToList();

            var vectors = batch.Select(p => new Vector
            {
                Id = $"{tenantId}-{p.productId}",
                Values = p.embedding,
                Metadata = ConvertToMetadata(p.metadata)
            }).ToList();

            try
            {
                var response = await index.UpsertAsync(
                    new UpsertRequest
                    {
                        Vectors = vectors,
                        Namespace = tenantId
                    },
                    cancellationToken: cancellationToken);

                totalUpserted += (int)response.UpsertedCount;

                _logger.LogInformation(
                    "Upserted batch {Current}/{Total} products to Pinecone",
                    Math.Min(i + batchSize, products.Count),
                    products.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to upsert batch starting at index {Index}",
                    i);

                failedIds.AddRange(batch.Select(p => p.productId));
            }
        }

        return new UpsertResult
        {
            UpsertedCount = totalUpserted,
            FailedIds = failedIds
        };
    }

    public async Task<List<ProductSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK = 10,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantNamespace();
        var index = _client.Index(_options.IndexName);

        var request = new QueryRequest
        {
            Namespace = tenantId,
            Vector = queryEmbedding,
            TopK = (uint)topK,
            IncludeMetadata = true,
            IncludeValues = false
        };

        if (filters != null && filters.Count > 0)
        {
            request.Filter = ConvertToMetadata(filters);
        }

        try
        {
            var response = await index.QueryAsync(
                request,
                cancellationToken: cancellationToken);

            if (response.Matches == null || response.Matches.Count() == 0)
            {
                _logger.LogInformation(
                    "No similar products found for query in namespace {Namespace}",
                    tenantId);
                return new List<ProductSearchResult>();
            }

            var results = response.Matches
                .Where(m => m.Metadata != null)
                .Select(m => new ProductSearchResult
                {
                    ProductId = m.Metadata!["product_id"]?.ToString() ?? string.Empty,
                    Name = m.Metadata["name"]?.ToString() ?? string.Empty,
                    Category = m.Metadata["category"]?.ToString() ?? string.Empty,
                    Price = m.Metadata["price"] != null ? Convert.ToDecimal(m.Metadata["price"]) : 0,
                    Score = m.Score ?? 0f
                }).ToList();

            _logger.LogInformation(
                "Found {Count} similar products for query in namespace {Namespace}",
                results.Count, tenantId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to search similar products in Pinecone");
            throw;
        }
    }

    public async Task DeleteProductAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantNamespace();
        var vectorId = $"{tenantId}-{productId}";
        var index = _client.Index(_options.IndexName);

        try
        {
            await index.DeleteAsync(
                new DeleteRequest
                {
                    Ids = new[] { vectorId },
                    Namespace = tenantId
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Deleted product {ProductId} from Pinecone namespace {Namespace}",
                productId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete product {ProductId} from Pinecone",
                productId);
            throw;
        }
    }

    public async Task DeleteBatchAsync(
        List<string> productIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantNamespace();
        var vectorIds = productIds.Select(id => $"{tenantId}-{id}").ToArray();
        var index = _client.Index(_options.IndexName);

        try
        {
            await index.DeleteAsync(
                new DeleteRequest
                {
                    Ids = vectorIds,
                    Namespace = tenantId
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Deleted {Count} products from Pinecone namespace {Namespace}",
                productIds.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete batch of products from Pinecone");
            throw;
        }
    }

    private string GetTenantNamespace()
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            throw new InvalidOperationException(
                "Tenant context not resolved. Vector operations require tenant isolation.");
        }
        return _tenantContext.TenantId.Value.ToString();
    }

    private static Metadata ConvertToMetadata(Dictionary<string, object> dict)
    {
        var metadata = new Metadata();
        foreach (var kvp in dict)
        {
            // Implicit conversion via MetadataValue operators
            // Note: Pinecone only supports string, double, bool
            // Precision loss for decimal/long is acceptable for metadata filtering
            metadata[kvp.Key] = kvp.Value switch
            {
                string s => s,
                int i => (double)i,
                long l => (double)l,
                double d => d,
                float f => (double)f,
                decimal dec => (double)dec,
                bool b => b,
                _ => kvp.Value.ToString() ?? string.Empty
            };
        }
        return metadata;
    }
}
