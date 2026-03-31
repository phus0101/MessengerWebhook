using System.Net.Http.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services;

/// <summary>
/// Implementation of Facebook Messenger Send API
/// </summary>
public class MessengerService : IMessengerService
{
    private readonly HttpClient _httpClient;
    private readonly FacebookOptions _options;
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MessengerService> _logger;

    public MessengerService(
        HttpClient httpClient,
        IOptions<FacebookOptions> options,
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<MessengerService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text, CancellationToken cancellationToken = default)
    {
        var request = new SendMessageRequest(
            new SendRecipient(recipientId),
            new SendMessage(text)
        );

        var pageAccessToken = await ResolvePageAccessTokenAsync();
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={pageAccessToken}";

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Graph API error: {StatusCode} - {Error}",
                response.StatusCode,
                error);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException("Rate limit exceeded", null, response.StatusCode);
            }

            throw new HttpRequestException($"Graph API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public async Task<SendMessageResponse> SendQuickReplyAsync(
        string recipientId,
        string text,
        List<QuickReplyButton> quickReplies,
        CancellationToken cancellationToken = default)
    {
        if (quickReplies.Count > 13)
        {
            throw new ArgumentException("Facebook allows max 13 quick replies", nameof(quickReplies));
        }

        var request = new SendQuickReplyRequest(
            new SendRecipient(recipientId),
            new SendMessageWithQuickReplies(text, quickReplies)
        );

        var pageAccessToken = await ResolvePageAccessTokenAsync();
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={pageAccessToken}";

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Graph API error sending quick reply: {StatusCode} - {Error}",
                response.StatusCode,
                error);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException("Rate limit exceeded", null, response.StatusCode);
            }

            throw new HttpRequestException($"Graph API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public async Task<bool> IsVideoLiveAsync(string videoId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pageAccessToken = await ResolvePageAccessTokenAsync();
            var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{videoId}?fields=status,live_status&access_token={pageAccessToken}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check video status: {StatusCode}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var video = System.Text.Json.JsonSerializer.Deserialize<VideoStatus>(json);

            return video?.LiveStatus == "LIVE";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking video live status for {VideoId}", videoId);
            return false;
        }
    }

    public async Task<bool> HideCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pageAccessToken = await ResolvePageAccessTokenAsync();
            var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{commentId}?is_hidden=true&access_token={pageAccessToken}";
            var response = await _httpClient.PostAsync(url, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to hide comment {CommentId}: {StatusCode}", commentId, response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully hidden comment {CommentId}", commentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding comment {CommentId}", commentId);
            return false;
        }
    }

    public async Task<bool> ReplyToCommentAsync(string commentId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var pageAccessToken = await ResolvePageAccessTokenAsync();
            var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{commentId}/replies?access_token={pageAccessToken}";

            var payload = new { message };
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to reply to comment {CommentId}: {StatusCode} - {Error}", commentId, response.StatusCode, error);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new HttpRequestException("Rate limit exceeded", null, response.StatusCode);
                }

                return false;
            }

            _logger.LogInformation("Successfully replied to comment {CommentId}", commentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to comment {CommentId}", commentId);
            return false;
        }
    }

    private async Task<string> ResolvePageAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_tenantContext.FacebookPageId))
        {
            var overrideToken = await _dbContext.FacebookPageConfigs
                .IgnoreQueryFilters()
                .Where(x => x.FacebookPageId == _tenantContext.FacebookPageId && x.IsActive)
                .Select(x => x.PageAccessToken)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(overrideToken))
            {
                return overrideToken;
            }
        }

        if (string.IsNullOrWhiteSpace(_options.PageAccessToken))
        {
            throw new InvalidOperationException(
                "Facebook page access token is missing. Configure a default token or page-specific override.");
        }

        return _options.PageAccessToken;
    }

    private class VideoStatus
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("live_status")]
        public string LiveStatus { get; set; } = string.Empty;
    }
}
