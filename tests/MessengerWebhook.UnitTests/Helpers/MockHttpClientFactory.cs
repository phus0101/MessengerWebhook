using System.Net;

namespace MessengerWebhook.UnitTests.Helpers;

/// <summary>
/// Factory for creating mock HttpClient instances for testing
/// </summary>
public static class MockHttpClientFactory
{
    /// <summary>
    /// Create HttpClient that returns a successful response with given content
    /// </summary>
    public static HttpClient CreateWithResponse(HttpStatusCode statusCode, string content)
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.api.com")
        };
    }

    /// <summary>
    /// Create HttpClient that simulates a delay (for timeout testing)
    /// </summary>
    public static HttpClient CreateWithDelay(TimeSpan delay)
    {
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            await Task.Delay(delay, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.api.com")
        };
    }
}
