using System.Web;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.AI.Handlers;

public class GeminiAuthHandler : DelegatingHandler
{
    private readonly GeminiOptions _options;

    public GeminiAuthHandler(IOptions<GeminiOptions> options)
    {
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
            return await base.SendAsync(request, cancellationToken);

        // Add API key to query string
        var uriBuilder = new UriBuilder(request.RequestUri);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["key"] = _options.ApiKey;
        uriBuilder.Query = query.ToString();
        request.RequestUri = uriBuilder.Uri;

        return await base.SendAsync(request, cancellationToken);
    }
}
