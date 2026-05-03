using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// AI-based sub-intent classifier using Gemini Flash-Lite
/// Handles ambiguous queries that keyword detector cannot classify confidently
/// </summary>
public sealed class GeminiSubIntentClassifier : ISubIntentClassifier
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly SubIntentOptions _subIntentOptions;
    private readonly ILogger<GeminiSubIntentClassifier> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GeminiSubIntentClassifier(
        HttpClient httpClient,
        IOptions<GeminiOptions> geminiOptions,
        IOptions<SubIntentOptions> subIntentOptions,
        ILogger<GeminiSubIntentClassifier> logger)
    {
        _httpClient = httpClient;
        _geminiOptions = geminiOptions.Value;
        _subIntentOptions = subIntentOptions.Value;
        _logger = logger;
    }

    public async Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_subIntentOptions.ClassifierTimeoutMs));

        try
        {
            _logger.LogDebug("Classifying sub-intent for message: {Message}", message);

            var model = string.IsNullOrWhiteSpace(_geminiOptions.FlashLiteModel)
                ? _geminiOptions.ProModel
                : _geminiOptions.FlashLiteModel;

            var url = $"v1beta/models/{model}:generateContent";
            if (!string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
            {
                url += $"?key={Uri.EscapeDataString(_geminiOptions.ApiKey)}";
            }

            var request = BuildRequest(message, conversationContext);
            var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini sub-intent classifier returned HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(JsonOptions, timeoutCts.Token);
            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var classification = JsonSerializer.Deserialize<GeminiClassifierResponse>(ExtractJson(text), JsonOptions);

            if (classification == null || classification.Confidence < _subIntentOptions.MinConfidence)
            {
                if (classification != null)
                {
                    _logger.LogDebug("Gemini confidence {Confidence} below threshold {Threshold}",
                        classification.Confidence, _subIntentOptions.MinConfidence);
                }
                return null;
            }

            var result = MapToSubIntentResult(classification);
            if (result != null)
            {
                _logger.LogInformation(
                    "Sub-intent classified: {Category} (confidence: {Confidence}, source: ai)",
                    result.Category,
                    result.Confidence);
            }

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini sub-intent classifier timed out after {TimeoutMs}ms",
                _subIntentOptions.ClassifierTimeoutMs);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini sub-intent classifier failed");
            return null;
        }
    }

    private object BuildRequest(string message, ConversationContext? context)
    {
        var prompt = BuildPrompt(message, context);

        return new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 150
            }
        };
    }

    private string BuildPrompt(string message, ConversationContext? context)
    {
        var contextInfo = context != null
            ? $"Context: State={context.CurrentState}, HasProduct={context.HasProduct}, DominantTopic={context.DominantTopic}"
            : "Context: None";

        var historyContext = context?.RecentHistory?.Count > 0
            ? BuildHistoryContext(context.RecentHistory)
            : "No recent history";

        return $@"You are a Vietnamese e-commerce intent classifier.

Customer message: ""{message}""
{contextInfo}

Recent conversation:
{historyContext}

Task: Classify customer sub-intent into ONE category:
- product_question: asking about product features, ingredients, usage
- price_question: asking about price, cost, discounts
- shipping_question: asking about delivery time, shipping cost, tracking
- policy_question: asking about return, refund, warranty policies
- availability_question: asking if product is in stock
- comparison_question: comparing multiple products
- none: no specific sub-intent detected

IMPORTANT: Return ONLY valid JSON:
{{
  ""subIntent"": ""product_question|price_question|shipping_question|policy_question|availability_question|comparison_question|none"",
  ""confidence"": 0.0-1.0,
  ""reason"": ""brief explanation in English"",
  ""matchedKeywords"": [""keyword1"", ""keyword2""]
}}

Few-shot examples:
- ""sản phẩm này có chứa paraben không?"" → product_question (asking about ingredients)
- ""giá bao nhiêu vậy?"" → price_question (direct price inquiry)
- ""ship mất bao lâu?"" → shipping_question (delivery time)
- ""có freeship không?"" → shipping_question (shipping cost)
- ""còn hàng không?"" → availability_question (stock inquiry)
- ""em muốn mua combo"" → none (buying intent, not a question)
- ""ok"" (after bot asked about shipping) → none (affirmation, context-dependent)";
    }

    private string BuildHistoryContext(List<ConversationMessage> history)
    {
        var recent = history.TakeLast(3);
        var lines = recent.Select(m => $"{m.Role}: {m.Content}");
        return string.Join("\n", lines);
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (end <= 3)
        {
            return trimmed;
        }

        var body = trimmed[3..end].Trim();
        if (body.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            body = body[4..].Trim();
        }

        return body;
    }

    private static SubIntentResult? MapToSubIntentResult(GeminiClassifierResponse response)
    {
        var category = response.SubIntent?.Trim().ToLowerInvariant() switch
        {
            "product_question" => SubIntentCategory.ProductQuestion,
            "price_question" => SubIntentCategory.PriceQuestion,
            "shipping_question" => SubIntentCategory.ShippingQuestion,
            "policy_question" => SubIntentCategory.PolicyQuestion,
            "availability_question" => SubIntentCategory.AvailabilityQuestion,
            "comparison_question" => SubIntentCategory.ComparisonQuestion,
            "none" => SubIntentCategory.None,
            _ => (SubIntentCategory?)null
        };

        if (category == null)
        {
            return null;
        }

        return SubIntentResult.Create(
            category: category.Value,
            confidence: response.Confidence,
            matchedKeywords: response.MatchedKeywords ?? Array.Empty<string>(),
            explanation: response.Reason ?? string.Empty,
            source: "ai");
    }

    private sealed class GeminiClassifierResponse
    {
        public string? SubIntent { get; set; }
        public decimal Confidence { get; set; }
        public string? Reason { get; set; }
        public string[]? MatchedKeywords { get; set; }
    }

    private sealed class GeminiGenerateContentResponse
    {
        public GeminiCandidate[]? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        public GeminiPart[]? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
