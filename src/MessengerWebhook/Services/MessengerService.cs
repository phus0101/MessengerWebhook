using System.Net.Http.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services;

/// <summary>
/// Implementation of Facebook Messenger Send API
/// </summary>
public class MessengerService : IMessengerService
{
    private readonly HttpClient _httpClient;
    private readonly FacebookOptions _options;
    private readonly ILogger<MessengerService> _logger;

    public MessengerService(
        HttpClient httpClient,
        IOptions<FacebookOptions> options,
        ILogger<MessengerService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text)
    {
        var request = new SendMessageRequest(
            new SendRecipient(recipientId),
            new SendMessage(text)
        );

        var url = $"https://graph.facebook.com/v21.0/me/messages?access_token={_options.PageAccessToken}";

        var response = await _httpClient.PostAsJsonAsync(url, request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
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

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        return result ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
