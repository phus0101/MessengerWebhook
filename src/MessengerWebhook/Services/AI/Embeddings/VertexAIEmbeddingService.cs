using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.AI.Embeddings;

/// <summary>
/// Generates text embeddings using Google Cloud Vertex AI text-embedding-004 model
/// </summary>
public class VertexAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly VertexAIOptions _options;
    private readonly ILogger<VertexAIEmbeddingService> _logger;
    private readonly string _endpoint;
    private GoogleCredential? _credential;

    public VertexAIEmbeddingService(
        HttpClient httpClient,
        IOptions<VertexAIOptions> options,
        ILogger<VertexAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _endpoint = $"https://{_options.Location}-aiplatform.googleapis.com/v1/" +
                   $"projects/{_options.ProjectId}/locations/{_options.Location}/" +
                   $"publishers/google/models/{_options.Model}:predict";

        InitializeAuthentication();
    }

    protected virtual void InitializeAuthentication()
    {
        try
        {
            _credential = GoogleCredential.FromFile(_options.ServiceAccountKeyPath)
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Cloud credentials from {Path}",
                _options.ServiceAccountKeyPath);
            throw;
        }
    }

    protected virtual async Task<string> GetAccessTokenAsync()
    {
        if (_credential == null)
        {
            throw new InvalidOperationException("Google Cloud credentials not initialized");
        }

        var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        return token;
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await EmbedBatchAsync(new List<string> { text }, cancellationToken);
        return embeddings[0];
    }

    public async Task<List<float[]>> EmbedBatchAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return new List<float[]>();
        }

        if (texts.Count > 100)
        {
            throw new ArgumentException(
                "Vertex AI supports max 100 texts per batch. Got: " + texts.Count,
                nameof(texts));
        }

        var request = new VertexAIRequest
        {
            Instances = texts.Select(text => new Instance
            {
                Content = text,
                TaskType = "RETRIEVAL_DOCUMENT"
            }).ToArray()
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Refresh token before request
        var accessToken = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.PostAsJsonAsync(
            _endpoint,
            request,
            cancellationToken);

        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Vertex AI API error: {StatusCode} - {Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Vertex AI API error: {(int)response.StatusCode} - {error}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<VertexAIResponse>(cancellationToken);

        if (result?.Predictions == null || result.Predictions.Length == 0)
        {
            throw new InvalidOperationException("Vertex AI returned empty predictions");
        }

        _logger.LogInformation(
            "Generated {Count} embeddings in {Ms}ms (avg: {AvgMs}ms/embedding)",
            texts.Count,
            stopwatch.ElapsedMilliseconds,
            stopwatch.ElapsedMilliseconds / texts.Count);

        return result.Predictions
            .Select(p => p.Embeddings.Values)
            .ToList();
    }

    private class VertexAIRequest
    {
        [JsonPropertyName("instances")]
        public Instance[] Instances { get; set; } = Array.Empty<Instance>();
    }

    private class Instance
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("task_type")]
        public string TaskType { get; set; } = string.Empty;
    }

    private class VertexAIResponse
    {
        [JsonPropertyName("predictions")]
        public Prediction[] Predictions { get; set; } = Array.Empty<Prediction>();
    }

    private class Prediction
    {
        [JsonPropertyName("embeddings")]
        public EmbeddingData Embeddings { get; set; } = new();
    }

    private class EmbeddingData
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}
