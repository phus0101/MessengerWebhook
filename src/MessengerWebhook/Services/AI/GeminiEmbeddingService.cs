using System.Net.Http.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Models;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.AI;

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));

        var request = new
        {
            model = "text-embedding-004",
            content = new { parts = new[] { new { text } } }
        };

        var url = "https://generativelanguage.googleapis.com/v1/models/text-embedding-004:embedContent";
        var response = await _httpClient.PostAsJsonAsync(url, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Embedding API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Embedding API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
        if (result?.Embedding?.Values == null || result.Embedding.Values.Length == 0)
        {
            throw new InvalidOperationException("Embedding API returned empty result");
        }

        _logger.LogDebug("Generated embedding with {Dimensions} dimensions", result.Embedding.Values.Length);
        return result.Embedding.Values;
    }

    public async Task<List<float[]>> GenerateBatchAsync(List<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts, nameof(texts));
        if (texts.Count == 0) return new List<float[]>();

        // Batch API: up to 100 texts per request
        var batches = texts.Chunk(100);
        var allEmbeddings = new List<float[]>();

        foreach (var batch in batches)
        {
            var request = new
            {
                requests = batch.Select(text => new
                {
                    model = "text-embedding-004",
                    content = new { parts = new[] { new { text } } }
                }).ToList()
            };

            var url = "https://generativelanguage.googleapis.com/v1/models/text-embedding-004:batchEmbedContents";
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Batch embedding API error: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Batch embedding API error: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<BatchEmbeddingResponse>(ct);
            if (result?.Embeddings == null)
            {
                throw new InvalidOperationException("Batch embedding API returned null result");
            }

            allEmbeddings.AddRange(result.Embeddings.Select(e => e.Embedding.Values));
        }

        _logger.LogInformation("Generated {Count} embeddings in {Batches} batches", allEmbeddings.Count, batches.Count());
        return allEmbeddings;
    }
}
