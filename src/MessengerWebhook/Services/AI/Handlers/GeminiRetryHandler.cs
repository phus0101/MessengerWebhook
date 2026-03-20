using System.Net;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;
using Polly;

namespace MessengerWebhook.Services.AI.Handlers;

public class GeminiRetryHandler : DelegatingHandler
{
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ILogger<GeminiRetryHandler> _logger;

    public GeminiRetryHandler(
        IOptions<GeminiOptions> options,
        ILogger<GeminiRetryHandler> logger)
    {
        _logger = logger;
        var maxRetries = options.Value.MaxRetries;
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Gemini API retry {RetryCount} after {Delay}ms. Status: {StatusCode}",
                        retryCount, timespan.TotalMilliseconds, outcome.Result.StatusCode);
                });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(
            () => base.SendAsync(request, cancellationToken));
    }
}
