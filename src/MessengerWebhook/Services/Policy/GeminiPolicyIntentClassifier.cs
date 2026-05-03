using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public sealed class GeminiPolicyIntentClassifier : IPolicyIntentClassifier
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly PolicyGuardOptions _policyOptions;
    private readonly ILogger<GeminiPolicyIntentClassifier> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GeminiPolicyIntentClassifier(
        HttpClient httpClient,
        IOptions<GeminiOptions> geminiOptions,
        IOptions<PolicyGuardOptions> policyOptions,
        ILogger<GeminiPolicyIntentClassifier> logger)
    {
        _httpClient = httpClient;
        _geminiOptions = geminiOptions.Value;
        _policyOptions = policyOptions.Value;
        _logger = logger;
    }

    public async Task<PolicyClassificationResult?> ClassifyAsync(
        PolicyGuardRequest request,
        string normalizedMessage,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_policyOptions.ClassifierTimeoutMs));

        try
        {
            var model = string.IsNullOrWhiteSpace(_geminiOptions.FlashLiteModel)
                ? _geminiOptions.ProModel
                : _geminiOptions.FlashLiteModel;
            var url = $"v1beta/models/{model}:generateContent";
            if (!string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
            {
                url += $"?key={Uri.EscapeDataString(_geminiOptions.ApiKey)}";
            }

            var response = await _httpClient.PostAsJsonAsync(url, BuildRequest(normalizedMessage), JsonOptions, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Policy semantic classifier returned HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(JsonOptions, timeoutCts.Token);
            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var classification = JsonSerializer.Deserialize<SemanticClassifierResponse>(ExtractJson(text), JsonOptions);
            if (classification == null || classification.Confidence < _policyOptions.SemanticClassifierMinConfidence)
            {
                return null;
            }

            return MapClassification(classification);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Policy semantic classifier failed.");
            return null;
        }
    }

    private object BuildRequest(string normalizedMessage)
    {
        return new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = "Classify this customer message into exactly one category: manual_review, unsupported_question, policy_exception, refund_request, cancellation_request, prompt_injection, none, uncertain. Return only JSON with category, confidence, explanation, matchedSpans. Message: " + normalizedMessage
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0,
                maxOutputTokens = 256
            }
        };
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

    private static PolicyClassificationResult? MapClassification(SemanticClassifierResponse response)
    {
        var category = response.Category?.Trim().ToLowerInvariant();
        var intent = category switch
        {
            "manual_review" => PolicySemanticIntent.ManualReview,
            "unsupported_question" => PolicySemanticIntent.UnsupportedQuestion,
            "policy_exception" => PolicySemanticIntent.PolicyException,
            "refund_request" => PolicySemanticIntent.RefundRequest,
            "cancellation_request" => PolicySemanticIntent.CancellationRequest,
            "prompt_injection" => PolicySemanticIntent.PromptInjection,
            _ => PolicySemanticIntent.None
        };

        if (intent == PolicySemanticIntent.None)
        {
            return null;
        }

        return new PolicyClassificationResult(
            intent,
            response.Confidence,
            MapReason(intent),
            response.Explanation ?? string.Empty,
            response.MatchedSpans ?? []);
    }

    private static SupportCaseReason MapReason(PolicySemanticIntent intent)
    {
        return intent switch
        {
            PolicySemanticIntent.ManualReview => SupportCaseReason.ManualReview,
            PolicySemanticIntent.UnsupportedQuestion => SupportCaseReason.UnsupportedQuestion,
            PolicySemanticIntent.PolicyException => SupportCaseReason.PolicyException,
            PolicySemanticIntent.RefundRequest => SupportCaseReason.RefundRequest,
            PolicySemanticIntent.CancellationRequest => SupportCaseReason.CancellationRequest,
            PolicySemanticIntent.PromptInjection => SupportCaseReason.PromptInjection,
            _ => SupportCaseReason.ManualReview
        };
    }

    private sealed class SemanticClassifierResponse
    {
        public string? Category { get; set; }
        public decimal Confidence { get; set; }
        public string? Explanation { get; set; }
        public string[]? MatchedSpans { get; set; }
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
