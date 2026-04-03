using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.AI.Strategies;
using Microsoft.Extensions.Options;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.AI;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IModelSelectionStrategy _modelStrategy;
    private readonly ILogger<GeminiService> _logger;
    private readonly string _systemPrompt;

    public GeminiService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        IModelSelectionStrategy modelStrategy,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _modelStrategy = modelStrategy;
        _logger = logger;

        // Load system prompt from file
        var promptPath = Path.Combine(AppContext.BaseDirectory, _options.SystemPromptPath);
        if (File.Exists(promptPath))
        {
            _systemPrompt = File.ReadAllText(promptPath);
            _logger.LogInformation("Loaded system prompt from {Path}", promptPath);
        }
        else
        {
            _logger.LogWarning("System prompt file not found at {Path}, using default", promptPath);
            _systemPrompt = GetDefaultSystemPrompt();
        }
    }

    public async Task<string> SendMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        string? ragContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));
        ArgumentNullException.ThrowIfNull(history, nameof(history));

        if (message.Length > 10000)
        {
            _logger.LogWarning("Message too long: {Length} chars for user {UserId}",
                message.Length, userId);
            throw new ArgumentException("Message exceeds maximum length of 10000 characters", nameof(message));
        }

        var model = modelOverride ?? _modelStrategy.SelectModel(message);
        var modelName = model == GeminiModelType.Pro
            ? _options.ProModel
            : _options.FlashLiteModel;

        _logger.LogInformation(
            "Sending message to Gemini {Model} for user {UserId}. Message length: {Length}",
            modelName, userId, message.Length);

        // Build request
        var request = new
        {
            contents = BuildContents(message, history, ragContext),
            generationConfig = new
            {
                temperature = _options.Temperature,
                maxOutputTokens = _options.MaxTokens
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1/models/{modelName}:generateContent";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API error: {StatusCode} - {Error}",
                response.StatusCode, error);
            throw new HttpRequestException($"Gemini API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken);

        if (result?.Candidates == null || result.Candidates.Length == 0)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini returned no candidates. Response: {Response}", errorContent);
            throw new InvalidOperationException("Gemini API returned no response candidates");
        }

        var candidate = result.Candidates[0];
        if (candidate?.Content?.Parts == null || candidate.Content.Parts.Length == 0)
        {
            _logger.LogError("Gemini candidate has no content parts. FinishReason: {Reason}",
                candidate?.FinishReason);
            throw new InvalidOperationException($"Gemini response incomplete. Reason: {candidate?.FinishReason}");
        }

        var responseText = candidate.Content.Parts[0].Text;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("Gemini returned empty response text");
        }

        _logger.LogInformation(
            "Received response from Gemini. Tokens: {Tokens}",
            result.UsageMetadata?.TotalTokenCount ?? 0);

        return responseText;
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming implementation placeholder
        // For now, return the full response as a single chunk
        var response = await SendMessageAsync(userId, message, history, modelOverride, null, cancellationToken);
        yield return response;
    }

    public GeminiModelType SelectModel(string message)
    {
        return _modelStrategy.SelectModel(message);
    }

    private string GetSystemPrompt()
    {
        return _systemPrompt;
    }

    private string GetDefaultSystemPrompt()
    {
        return @"Bạn là chuyên viên tư vấn mỹ phẩm chuyên nghiệp.
Nhiệm vụ: Giúp khách hàng tìm sản phẩm phù hợp với loại da và tình trạng da.
Quy tắc: Trả lời ngắn gọn (2-3 câu), đặt câu hỏi làm rõ nhu cầu, KHÔNG tự tạo thông tin sản phẩm.";
    }

    public async Task<ConfirmationDetectionResult> DetectConfirmationAsync(
        string message,
        string contextPhone,
        string contextAddress,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableAiConfirmationDetection)
        {
            return new ConfirmationDetectionResult
            {
                IsConfirming = false,
                Confidence = 0.0,
                Reason = "AI confirmation detection is disabled",
                DetectionMethod = "fallback"
            };
        }

        var prompt = $@"You are a Vietnamese customer service intent classifier.

Customer message: ""{message}""
Context: Customer previously provided phone={contextPhone}, address={contextAddress}. Bot asked if they want to reuse this info.

Task: Determine if the customer is CONFIRMING they want to reuse the remembered contact info.

Confirmation examples:
- ""dung roi"" (yes correct)
- ""ok em"" (ok)
- ""van dung"" (still use it)
- ""len don luon"" (create order now)

NOT confirmation examples:
- ""ship bao lau?"" (question about shipping time)
- ""ship nhanh khong?"" (question about fast shipping)
- ""gia bao nhieu?"" (question about price)

Respond ONLY with valid JSON:
{{
  ""isConfirming"": true/false,
  ""confidence"": 0.0-1.0,
  ""reason"": ""brief explanation in English""
}}";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            var request = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 100
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1/models/{_options.FlashLiteModel}:generateContent";
            var response = await _httpClient.PostAsJsonAsync(url, request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini confirmation detection API error: {StatusCode}", response.StatusCode);
                return FallbackResult("API error");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cts.Token);
            var responseText = result?.Candidates?[0]?.Content?.Parts?[0]?.Text;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return FallbackResult("Empty response");
            }

            var jsonResult = System.Text.Json.JsonSerializer.Deserialize<ConfirmationDetectionResult>(responseText);
            if (jsonResult == null)
            {
                return FallbackResult("Invalid JSON");
            }

            jsonResult.DetectionMethod = "ai-reasoning";
            _logger.LogInformation(
                "AI confirmation detection: IsConfirming={IsConfirming}, Confidence={Confidence}, Message='{Message}'",
                jsonResult.IsConfirming, jsonResult.Confidence, message);

            return jsonResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI confirmation detection timeout for message: '{Message}'", message);
            return FallbackResult("Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI confirmation detection error for message: '{Message}'", message);
            return FallbackResult($"Exception: {ex.Message}");
        }
    }

    private static ConfirmationDetectionResult FallbackResult(string reason)
    {
        return new ConfirmationDetectionResult
        {
            IsConfirming = false,
            Confidence = 0.0,
            Reason = reason,
            DetectionMethod = "fallback"
        };
    }

    public async Task<IntentDetectionResult> DetectIntentAsync(
        string message,
        ConversationState currentState,
        bool hasProduct,
        bool hasContact,
        List<AiConversationMessage>? recentHistory = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableAiIntentDetection)
        {
            return IntentFallbackResult("AI intent detection is disabled");
        }

        // Build history context from last 3 messages
        var historyContext = string.Empty;
        if (recentHistory != null && recentHistory.Count > 0)
        {
            var last3 = recentHistory.TakeLast(3);
            var lines = new List<string> { "Recent conversation:" };
            foreach (var msg in last3)
            {
                var speaker = msg.Role == "assistant" ? "Bot" : "Customer";
                lines.Add($"{speaker}: \"{msg.Content}\"");
            }
            historyContext = string.Join("\n", lines);
        }

        var prompt = $@"You are a Vietnamese customer service intent classifier.

Customer message: ""{message}""
Context: State={currentState}, HasProduct={hasProduct}, HasContact={hasContact}

{historyContext}

Task: Classify customer intent into ONE of these categories:

1. Browsing - exploring products, not ready to buy
2. Consulting - needs advice/information before buying
3. ReadyToBuy - ready to place order NOW
4. Confirming - confirming previous info
5. Questioning - asking questions

INTENT DETECTION FRAMEWORK:

Analyze the VERB and CONTEXT to determine intent:

**Consulting Intent** - Verbs of information seeking:
- ""muốn + [tìm hiểu/biết/hỏi/xem]"" → Consulting
- ""cho em + [biết/giải thích/tư vấn]"" → Consulting
- ""em cần + [tư vấn/hỏi/biết thêm]"" → Consulting
Examples: ""muốn tìm hiểu về combo"", ""cho em biết thêm"", ""tư vấn giúp em""

**ReadyToBuy Intent** - Verbs of action/commitment:
- ""muốn + [mua/đặt/lấy/chốt]"" → ReadyToBuy
- ""em + [lên đơn/đặt hàng/mua luôn]"" → ReadyToBuy
- Declining consultation (""không"" after bot asks ""cần tư vấn thêm không?"") → ReadyToBuy
- **Affirmative responses in buying context**: When bot asks about ordering/buying and customer responds with short affirmations → ReadyToBuy
  * Context: Bot just asked ""Chị có muốn em lên đơn không?"" or mentioned product + price
  * Customer replies: ""ok"", ""được"", ""đồng ý"", ""oke"", ""yes"", ""uh"", ""uhm"", ""vâng"", or similar short confirmations
  * Key: The PREVIOUS bot message must be about ordering/buying, not just general questions
Examples: ""muốn mua combo"", ""lên đơn luôn"", ""không cần tư vấn""
Bot: ""Chị có muốn em lên đơn luôn không ạ?"" → Customer: ""ok e"" → ReadyToBuy

**Context is CRITICAL:**
- Same word, different intent based on verb:
  * ""muốn tìm hiểu"" = Consulting (information seeking)
  * ""muốn mua"" = ReadyToBuy (action)
- Conversation history matters:
  * ""không"" after consultation question = ReadyToBuy (declining advice)
  * ""không"" in other contexts = analyze the full sentence

**Few-shot examples:**
- ""chị có nghe bên em có gói combo, chị muốn tìm hiểu về gói đó"" → Consulting (verb: tìm hiểu = seeking info)
- ""em muốn mua gói combo"" → ReadyToBuy (verb: mua = action)
- Bot: ""Chị cần tư vấn thêm không?"" → Customer: ""không, em lên đơn"" → ReadyToBuy (declining consultation)
- ""cho em biết thêm về sản phẩm"" → Consulting (verb: biết = seeking info)

Respond ONLY with valid JSON:
{{
  ""intent"": ""Browsing|Consulting|ReadyToBuy|Confirming|Questioning"",
  ""confidence"": 0.0-1.0,
  ""reason"": ""brief explanation in English""
}}";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            var request = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 150
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1/models/{_options.FlashLiteModel}:generateContent";
            var response = await _httpClient.PostAsJsonAsync(url, request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini intent detection API error: {StatusCode}", response.StatusCode);
                return IntentFallbackResult("API error");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cts.Token);
            var responseText = result?.Candidates?[0]?.Content?.Parts?[0]?.Text;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return IntentFallbackResult("Empty response");
            }

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var jsonResult = System.Text.Json.JsonSerializer.Deserialize<IntentDetectionResult>(responseText, jsonOptions);
            if (jsonResult == null)
            {
                return IntentFallbackResult("Invalid JSON");
            }

            jsonResult.DetectionMethod = "ai-reasoning";
            _logger.LogInformation(
                "AI intent detection: Intent={Intent}, Confidence={Confidence}, Message='{Message}'",
                jsonResult.Intent, jsonResult.Confidence, message);

            return jsonResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI intent detection timeout for message: '{Message}'", message);
            return IntentFallbackResult("Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI intent detection error for message: '{Message}'", message);
            return IntentFallbackResult($"Exception: {ex.Message}");
        }
    }

    private static IntentDetectionResult IntentFallbackResult(string reason)
    {
        return new IntentDetectionResult
        {
            Intent = CustomerIntent.Consulting,
            Confidence = 0.0,
            Reason = reason,
            DetectionMethod = "fallback"
        };
    }

    private object[] BuildContents(string message, List<ConversationMessage> history, string? ragContext = null)
    {
        var contents = new List<object>();

        // Build system prompt with optional RAG context
        var systemPrompt = GetSystemPrompt();

        // Replace {RAG_CONTEXT} placeholder with actual RAG context
        if (!string.IsNullOrEmpty(ragContext))
        {
            systemPrompt = systemPrompt.Replace("{RAG_CONTEXT}", ragContext);
        }
        else
        {
            // If no RAG context, remove the placeholder
            systemPrompt = systemPrompt.Replace("{RAG_CONTEXT}", "Chưa có thông tin sản phẩm cụ thể.");
        }

        // Always add system prompt as first user message to ensure language detection works
        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = systemPrompt } }
        });
        contents.Add(new
        {
            role = "model",
            parts = new[] { new { text = "Tôi hiểu. Tôi sẽ tư vấn mỹ phẩm chuyên nghiệp, ngắn gọn và không tự tạo thông tin sản phẩm." } }
        });

        // Add history (limit to last 10 messages to control token usage)
        var historyToSend = history.TakeLast(10).ToList();
        if (history.Count > 10)
        {
            _logger.LogInformation(
                "Truncating conversation history from {Total} to {Kept} messages",
                history.Count, historyToSend.Count);
        }

        foreach (var msg in historyToSend)
        {
            // Map role: "assistant" -> "model" for Gemini API compatibility
            var role = msg.Role == "assistant" ? "model" : msg.Role;
            contents.Add(new
            {
                role = role,
                parts = new[] { new { text = msg.Content } }
            });
        }

        // Add current message
        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = message } }
        });

        return contents.ToArray();
    }
}
