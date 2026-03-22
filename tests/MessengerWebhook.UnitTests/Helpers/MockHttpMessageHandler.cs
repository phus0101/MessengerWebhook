using System.Net;

namespace MessengerWebhook.UnitTests.Helpers;

/// <summary>
/// Mock HttpMessageHandler for testing HttpClient-based services
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsyncFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsyncFunc)
    {
        _sendAsyncFunc = sendAsyncFunc ?? throw new ArgumentNullException(nameof(sendAsyncFunc));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _sendAsyncFunc(request, cancellationToken);
    }

    /// <summary>
    /// Create a handler that returns a successful JSON response
    /// </summary>
    public static MockHttpMessageHandler CreateWithJsonResponse(string jsonContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });
    }

    /// <summary>
    /// Create a handler that returns an error response
    /// </summary>
    public static MockHttpMessageHandler CreateWithError(HttpStatusCode statusCode, string errorContent = "")
    {
        return new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(errorContent, System.Text.Encoding.UTF8, "text/plain")
            };
            return Task.FromResult(response);
        });
    }
}
