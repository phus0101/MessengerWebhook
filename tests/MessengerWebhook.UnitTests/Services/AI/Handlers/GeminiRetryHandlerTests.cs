using System.Net;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace MessengerWebhook.UnitTests.Services.AI.Handlers;

public class GeminiRetryHandlerTests
{
    private readonly GeminiOptions _options;
    private readonly Mock<ILogger<GeminiRetryHandler>> _loggerMock;

    public GeminiRetryHandlerTests()
    {
        _options = new GeminiOptions
        {
            MaxRetries = 3
        };
        _loggerMock = new Mock<ILogger<GeminiRetryHandler>>();
    }

    [Fact]
    public async Task SendAsync_SuccessfulRequest_NoRetry()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}", HttpStatusCode.OK);
        var retryHandler = new GeminiRetryHandler(Options.Create(_options), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_BadRequest_NoRetry()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(
            "{\"error\":\"bad request\"}",
            HttpStatusCode.BadRequest);

        var retryHandler = new GeminiRetryHandler(Options.Create(_options), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendAsync_InternalServerError_NoRetry()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(
            "{\"error\":\"internal error\"}",
            HttpStatusCode.InternalServerError);

        var retryHandler = new GeminiRetryHandler(Options.Create(_options), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SendAsync_TooManyRequests_RetriesAndSucceeds()
    {
        // Arrange
        var callCount = 0;
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            callCount++;
            var statusCode = callCount < 3 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        var retryHandler = new GeminiRetryHandler(Options.Create(_options), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(3); // 2 failures + 1 success
    }

    [Fact]
    public async Task SendAsync_ServiceUnavailable_RetriesAndSucceeds()
    {
        // Arrange
        var callCount = 0;
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            callCount++;
            var statusCode = callCount < 2 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        var retryHandler = new GeminiRetryHandler(Options.Create(_options), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(2); // 1 failure + 1 success
    }

    [Fact]
    public async Task SendAsync_MaxRetriesExceeded_ReturnsLastFailure()
    {
        // Arrange
        var callCount = 0;
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            callCount++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":\"rate limit\"}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        var retryHandler = new GeminiRetryHandler(Options.Create(_options), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        callCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task SendAsync_ZeroMaxRetries_NoRetry()
    {
        // Arrange
        var optionsNoRetry = new GeminiOptions { MaxRetries = 0 };
        var callCount = 0;
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            callCount++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        var retryHandler = new GeminiRetryHandler(Options.Create(optionsNoRetry), _loggerMock.Object)
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(retryHandler);

        // Act
        var response = await client.GetAsync("https://api.example.com/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        callCount.Should().Be(1); // No retries
    }
}
