using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MessengerWebhook.Data;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// BM25 keyword search over product catalog for exact product code matching
/// </summary>
public class KeywordSearchService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<KeywordSearchService> _logger;

    public KeywordSearchService(
        MessengerBotDbContext dbContext,
        ILogger<KeywordSearchService> logger)
        : this(dbContext, new NullTenantContext(), logger)
    {
    }

    public KeywordSearchService(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<KeywordSearchService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// BM25 keyword search over product catalog
    /// </summary>
    public async Task<List<ProductSearchResult>> SearchAsync(
        string query,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        return await SearchAsync(query, topK, tenantId: null, cancellationToken);
    }

    public async Task<List<ProductSearchResult>> SearchAsync(
        string query,
        int topK,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var queryTokens = Tokenize(query);

        if (queryTokens.Count == 0)
        {
            _logger.LogWarning("Empty query after tokenization: {Query}", query);
            return new List<ProductSearchResult>();
        }

        var resolvedTenantId = tenantId ?? _tenantContext.TenantId;
        if (!resolvedTenantId.HasValue)
        {
            _logger.LogWarning("Keyword search skipped because tenant context is not resolved");
            return new List<ProductSearchResult>();
        }

        var productQuery = _dbContext.Products
            .Where(p => p.IsActive && p.TenantId == resolvedTenantId.Value);

        var products = await productQuery
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.Category,
                p.BasePrice,
                p.TenantId,
                Text = p.Name + " " + (p.Description ?? "") + " " + p.Code
            })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            _logger.LogWarning("No products found in database");
            return new List<ProductSearchResult>();
        }

        // Calculate BM25 scores
        var scores = products.Select(p =>
        {
            var docTokens = Tokenize(p.Text);
            var score = CalculateBM25(queryTokens, docTokens, products.Count);

            return new ProductSearchResult
            {
                ProductId = p.Id,
                Name = p.Name,
                Category = p.Category.ToString(),
                Price = p.BasePrice,
                Score = (float)score
            };
        })
        .Where(r => r.Score > 0)
        .OrderByDescending(r => r.Score)
        .Take(topK)
        .ToList();

        _logger.LogInformation(
            "Keyword search: {Query} → {Count} results",
            query,
            scores.Count);

        return scores;
    }

    private List<string> Tokenize(string text)
    {
        var tokens = Regex.Split(NormalizeVietnameseText(text), @"\W+")
            .Where(t => t.Length > 1)
            .ToList();

        return tokens;
    }

    private static string NormalizeVietnameseText(string text)
    {
        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character == 'đ' ? 'd' : character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private double CalculateBM25(
        List<string> queryTokens,
        List<string> docTokens,
        int totalDocs,
        double k1 = 1.5,
        double b = 0.75)
    {
        var avgDocLength = 50.0; // Approximate average document length
        var docLength = docTokens.Count;

        var score = 0.0;
        foreach (var term in queryTokens)
        {
            var termFreq = docTokens.Count(t => t == term);
            if (termFreq == 0) continue;

            // IDF calculation (simplified)
            var docsWithTerm = 1; // Simplified: assume term appears in 1 doc
            var idf = Math.Log((totalDocs - docsWithTerm + 0.5) / (docsWithTerm + 0.5) + 1);

            // BM25 formula
            var numerator = termFreq * (k1 + 1);
            var denominator = termFreq + k1 * (1 - b + b * (docLength / avgDocLength));

            score += idf * (numerator / denominator);
        }

        return score;
    }
}
