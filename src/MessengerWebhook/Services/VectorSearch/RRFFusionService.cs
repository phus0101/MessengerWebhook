namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Implements Reciprocal Rank Fusion (RRF) algorithm to merge results from multiple search systems
/// </summary>
public class RRFFusionService
{
    private readonly int _k;
    private readonly ILogger<RRFFusionService> _logger;

    public RRFFusionService(
        IConfiguration configuration,
        ILogger<RRFFusionService> logger)
    {
        _k = configuration.GetValue<int>("RRF:K", 60);
        _logger = logger;
    }

    /// <summary>
    /// Merge multiple ranked lists using Reciprocal Rank Fusion
    /// Formula: RRF_score(item) = Σ [ 1 / (k + rank_in_list) ]
    /// </summary>
    public List<FusedResult> Fuse(
        List<List<ProductSearchResult>> rankedLists,
        int topK = 5)
    {
        var scoreMap = new Dictionary<string, FusedResult>();

        // Calculate RRF scores
        foreach (var (list, listIndex) in rankedLists.Select((l, i) => (l, i)))
        {
            foreach (var (result, rank) in list.Select((r, i) => (r, i + 1)))
            {
                var rrfScore = 1.0 / (_k + rank);

                if (!scoreMap.ContainsKey(result.ProductId))
                {
                    scoreMap[result.ProductId] = new FusedResult
                    {
                        ProductId = result.ProductId,
                        Name = result.Name,
                        Category = result.Category,
                        Price = result.Price,
                        RRFScore = 0,
                        SourceScores = new Dictionary<string, float>(),
                        SourceRanks = new Dictionary<string, int>()
                    };
                }

                var fusedResult = scoreMap[result.ProductId];
                fusedResult.RRFScore += rrfScore;
                fusedResult.SourceScores[$"list_{listIndex}"] = result.Score;
                fusedResult.SourceRanks[$"list_{listIndex}"] = rank;
            }
        }

        // Sort by RRF score and return top-K
        var results = scoreMap.Values
            .OrderByDescending(r => r.RRFScore)
            .Take(topK)
            .ToList();

        _logger.LogInformation(
            "RRF fusion: {InputLists} lists → {OutputCount} results (k={K})",
            rankedLists.Count,
            results.Count,
            _k);

        return results;
    }
}

public class FusedResult
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double RRFScore { get; set; }

    /// <summary>
    /// Original scores from each search system (e.g., "list_0" for vector, "list_1" for keyword)
    /// </summary>
    public Dictionary<string, float> SourceScores { get; set; } = new();

    /// <summary>
    /// Rank positions from each search system (1-indexed)
    /// </summary>
    public Dictionary<string, int> SourceRanks { get; set; } = new();
}
