using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.AI.Strategies;
using Microsoft.Extensions.Options;

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
            contents = BuildContents(message, history),
            systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } },
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
        var response = await SendMessageAsync(userId, message, history, modelOverride, cancellationToken);
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

    private object[] BuildContents(string message, List<ConversationMessage> history)
    {
        var contents = new List<object>();

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
            contents.Add(new
            {
                role = msg.Role,
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
