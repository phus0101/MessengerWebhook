using System.Security.Cryptography;
using System.Text;

namespace MessengerWebhook.Services.Cache;

/// <summary>
/// Generates consistent SHA256-based cache keys for embeddings, results, and responses
/// </summary>
public class CacheKeyGenerator
{
    /// <summary>
    /// Generate cache key for embedding by query text
    /// </summary>
    public string GenerateEmbeddingKey(string text)
    {
        var hash = ComputeSHA256(text);
        return $"emb:{hash}";
    }

    /// <summary>
    /// Generate cache key for search results by embedding + tenant + filter
    /// </summary>
    public string GenerateResultKey(
        float[] embedding,
        Guid tenantId,
        Dictionary<string, object>? filter = null)
    {
        var embeddingHash = ComputeSHA256(SerializeEmbedding(embedding));
        var filterHash = filter != null
            ? ComputeSHA256(SerializeFilter(filter))
            : "none";

        return $"results:{embeddingHash}:{tenantId}:{filterHash}";
    }

    /// <summary>
    /// Generate cache key for LLM response by query + context + products
    /// </summary>
    public string GenerateResponseKey(
        string query,
        string context,
        List<string> productIds)
    {
        var combined = $"{query}|{context}|{string.Join(",", productIds)}";
        var hash = ComputeSHA256(combined);
        return $"response:{hash}";
    }

    private string ComputeSHA256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    private string SerializeEmbedding(float[] embedding)
    {
        return string.Join(",", embedding.Select(f => f.ToString("F6")));
    }

    private string SerializeFilter(Dictionary<string, object> filter)
    {
        return string.Join("|",
            filter.OrderBy(kv => kv.Key)
                  .Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
