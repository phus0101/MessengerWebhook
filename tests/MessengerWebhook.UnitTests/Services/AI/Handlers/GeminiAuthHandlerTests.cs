using System.Net;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.UnitTests.Helpers;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;

namespace MessengerWebhook.UnitTests.Services.AI.Handlers;

public class GeminiAuthHandlerTests
{
    private readonly GeminiOptions _options;

    public GeminiAuthHandlerTests()
    {
        _options = new GeminiOptions
        {
            ApiKey = "test-api-key-12345"
        };
    }

    [Fact]
    public async Task SendAsync_AddsApiKeyToQueryString()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}", HttpStatusCode.OK);
        var authHandler = new GeminiAuthHandler(Options.Create(_options))
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(authHandler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await client.SendAsync(request);

        // Assert
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should().Contain("key=test-api-key-12345");
    }

    [Fact]
    public async Task SendAsync_PreservesExistingQueryParameters()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}", HttpStatusCode.OK);
        var authHandler = new GeminiAuthHandler(Options.Create(_options))
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(authHandler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test?param1=value1&param2=value2");

        // Act
        await client.SendAsync(request);

        // Assert
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should().Contain("param1=value1");
        request.RequestUri!.Query.Should().Contain("param2=value2");
        request.RequestUri!.Query.Should().Contain("key=test-api-key-12345");
    }

    [Fact]
    public async Task SendAsync_WithBaseAddress_AddsApiKeyToQueryString()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}", HttpStatusCode.OK);
        var authHandler = new GeminiAuthHandler(Options.Create(_options))
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");

        // Act
        await client.SendAsync(request);

        // Assert
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should().Contain("key=test-api-key-12345");
    }

    [Fact]
    public async Task SendAsync_UrlEncodesApiKey()
    {
        // Arrange
        var optionsWithSpecialChars = new GeminiOptions
        {
            ApiKey = "key+with/special=chars"
        };

        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}", HttpStatusCode.OK);
        var authHandler = new GeminiAuthHandler(Options.Create(optionsWithSpecialChars))
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(authHandler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await client.SendAsync(request);

        // Assert
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should().Contain("key=");
        // URL encoding should handle special characters
        request.RequestUri!.Query.Should().NotContain("key+with/special=chars");
    }

    [Fact]
    public async Task SendAsync_WorksWithPostRequest()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}", HttpStatusCode.OK);
        var authHandler = new GeminiAuthHandler(Options.Create(_options))
        {
            InnerHandler = mockHandler
        };

        var client = new HttpClient(authHandler);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/test")
        {
            Content = new StringContent("{\"data\":\"test\"}")
        };

        // Act
        await client.SendAsync(request);

        // Assert
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should().Contain("key=test-api-key-12345");
    }
}
