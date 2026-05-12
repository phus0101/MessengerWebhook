using System.Net.Http.Json;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Notifications;

public class TelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(
        HttpClient httpClient,
        IOptions<TelegramOptions> options,
        ILogger<TelegramNotifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.BotToken)
            || string.IsNullOrWhiteSpace(_options.ChatId))
        {
            _logger.LogDebug("Telegram not configured, skipping notification");
            return;
        }

        try
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            var payload = new { chat_id = _options.ChatId, text = message, parse_mode = "HTML" };

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Telegram send failed: {StatusCode} {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            // Never let notification failure propagate — this is fire-and-forget
            _logger.LogWarning(ex, "Telegram notification failed");
        }
    }
}
