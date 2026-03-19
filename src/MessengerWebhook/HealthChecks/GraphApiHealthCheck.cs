using MessengerWebhook.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.HealthChecks;

public class GraphApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FacebookOptions _options;

    public GraphApiHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<FacebookOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/me?access_token={_options.PageAccessToken}";

            var response = await client.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Graph API accessible");
            }

            return HealthCheckResult.Unhealthy(
                $"Graph API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Graph API unreachable",
                ex);
        }
    }
}
