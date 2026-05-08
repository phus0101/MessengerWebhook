using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.SubIntent;

public class GeminiSubIntentClassifierTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly SubIntentOptions _subIntentOptions;

    public GeminiSubIntentClassifierTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        _geminiOptions = new GeminiOptions
        {
            ApiKey = "test-key",
            FlashLiteModel = "gemini-2.5-flash-lite"
        };

        _subIntentOptions = new SubIntentOptions
        {
            ClassifierTimeoutMs = 500,
            MinConfidence = 0.5m
        };
    }

    [Fact]
    public async Task ClassifyAsync_ValidResponse_ReturnsResult()
    {
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = "{\"subIntent\":\"product_question\",\"confidence\":0.85,\"reason\":\"asking about ingredients\",\"matchedKeywords\":[\"thành phần\"]}" }
                        }
                    }
                }
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(geminiResponse) });

        var classifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_subIntentOptions), NullLogger<GeminiSubIntentClassifier>.Instance);

        var result = await classifier.ClassifyAsync("thành phần có gì?");

        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.ProductQuestion);
        result.Confidence.Should().Be(0.85m);
        result.Source.Should().Be("ai");
    }

    [Fact]
    public async Task ClassifyAsync_Timeout_ReturnsNull()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var classifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_subIntentOptions), NullLogger<GeminiSubIntentClassifier>.Instance);

        var result = await classifier.ClassifyAsync("test message");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_LowConfidence_ReturnsNull()
    {
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = "{\"subIntent\":\"product_question\",\"confidence\":0.4,\"reason\":\"uncertain\",\"matchedKeywords\":[]}" }
                        }
                    }
                }
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(geminiResponse) });

        var classifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_subIntentOptions), NullLogger<GeminiSubIntentClassifier>.Instance);

        var result = await classifier.ClassifyAsync("test message");

        result.Should().BeNull();
    }
}
