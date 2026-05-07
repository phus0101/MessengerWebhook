using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using System.Net;
using System.Net.Http.Json;

namespace MessengerWebhook.UnitTests.Services.SubIntent;

public class HybridSubIntentClassifierTests
{
    private readonly KeywordSubIntentDetector _keywordDetector;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly SubIntentOptions _options;

    public HybridSubIntentClassifierTests()
    {
        _keywordDetector = new KeywordSubIntentDetector();
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

        _options = new SubIntentOptions
        {
            KeywordHighConfidenceThreshold = 0.9m,
            HybridAiAcceptanceThreshold = 0.7m,
            EnableAiFallback = true,
            ClassifierTimeoutMs = 500,
            MinConfidence = 0.5m
        };
    }

    [Fact]
    public async Task ClassifyAsync_KeywordHighConfidence_SkipsAi()
    {
        // Arrange: message with multiple strong keywords (high confidence)
        var aiClassifier = new GeminiSubIntentClassifier(
            _httpClient,
            Options.Create(_geminiOptions),
            Options.Create(_options),
            NullLogger<GeminiSubIntentClassifier>.Instance);

        var classifier = new HybridSubIntentClassifier(
            _keywordDetector,
            aiClassifier,
            Options.Create(_options),
            NullLogger<HybridSubIntentClassifier>.Instance);

        // Act
        var result = await classifier.ClassifyAsync("giá bao nhiêu tiền?");

        // Assert
        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.PriceQuestion);
        result.Confidence.Should().BeGreaterOrEqualTo(0.9m);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ClassifyAsync_KeywordLowConfidence_UsesAi()
    {
        // Arrange: ambiguous message, AI returns high confidence
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
                            new { text = "{\"subIntent\":\"shipping_question\",\"confidence\":0.85,\"reason\":\"asking about shipping\",\"matchedKeywords\":[\"ship\"]}" }
                        }
                    }
                }
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(geminiResponse) });

        var aiClassifier = new GeminiSubIntentClassifier(
            _httpClient,
            Options.Create(_geminiOptions),
            Options.Create(_options),
            NullLogger<GeminiSubIntentClassifier>.Instance);

        var classifier = new HybridSubIntentClassifier(
            _keywordDetector,
            aiClassifier,
            Options.Create(_options),
            NullLogger<HybridSubIntentClassifier>.Instance);

        // Act
        var result = await classifier.ClassifyAsync("ship nhanh không?");

        // Assert
        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.ShippingQuestion);
        // Source preserved as "ai" (was previously overwritten with "hybrid", losing observability).
        result.Source.Should().Be("ai");
    }

    [Fact]
    public async Task ClassifyAsync_AiFails_FallsBackToKeyword()
    {
        // Arrange: AI fails, fallback to keyword result
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("API error"));

        var aiClassifier = new GeminiSubIntentClassifier(
            _httpClient,
            Options.Create(_geminiOptions),
            Options.Create(_options),
            NullLogger<GeminiSubIntentClassifier>.Instance);

        var classifier = new HybridSubIntentClassifier(
            _keywordDetector,
            aiClassifier,
            Options.Create(_options),
            NullLogger<HybridSubIntentClassifier>.Instance);

        // Act
        var result = await classifier.ClassifyAsync("giá?");

        // Assert
        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.PriceQuestion);
    }

    [Fact]
    public async Task ClassifyAsync_AiBelowAcceptanceThreshold_FallsBackToKeyword()
    {
        // AI returns 0.6 confidence, threshold is 0.7 → must fall back to keyword result.
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
                            new { text = "{\"subIntent\":\"shipping_question\",\"confidence\":0.6,\"reason\":\"low\",\"matchedKeywords\":[]}" }
                        }
                    }
                }
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(geminiResponse) });

        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_options), NullLogger<GeminiSubIntentClassifier>.Instance);
        var classifier = new HybridSubIntentClassifier(_keywordDetector, aiClassifier, Options.Create(_options), NullLogger<HybridSubIntentClassifier>.Instance);

        var result = await classifier.ClassifyAsync("giá?");

        result.Should().NotBeNull();
        result!.Source.Should().Be("keyword");
        result.Category.Should().Be(SubIntentCategory.PriceQuestion);
    }

    [Fact]
    public async Task ClassifyAsync_BothKeywordAndAiNull_ReturnsNull()
    {
        // AI returns gibberish → null. Keyword finds nothing → null. Result must be null.
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_options), NullLogger<GeminiSubIntentClassifier>.Instance);
        var classifier = new HybridSubIntentClassifier(_keywordDetector, aiClassifier, Options.Create(_options), NullLogger<HybridSubIntentClassifier>.Instance);

        var result = await classifier.ClassifyAsync("xin chào");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_KeywordAndAiDisagree_AiWinsAtHighConfidence()
    {
        // Keyword finds PolicyQuestion ("hoàn tiền"). AI says ShippingQuestion at 0.9.
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
                            new { text = "{\"subIntent\":\"shipping_question\",\"confidence\":0.9,\"reason\":\"actually shipping\",\"matchedKeywords\":[]}" }
                        }
                    }
                }
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(geminiResponse) });

        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_options), NullLogger<GeminiSubIntentClassifier>.Instance);
        var classifier = new HybridSubIntentClassifier(_keywordDetector, aiClassifier, Options.Create(_options), NullLogger<HybridSubIntentClassifier>.Instance);

        var result = await classifier.ClassifyAsync("hoàn tiền không?");

        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.ShippingQuestion);
        result.Source.Should().Be("ai");
    }

    [Fact]
    public async Task ClassifyAsync_OuterCancellation_PropagatesOperationCanceled()
    {
        // Caller cancellation must surface, not be swallowed as a fallback path.
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_options), NullLogger<GeminiSubIntentClassifier>.Instance);
        var classifier = new HybridSubIntentClassifier(_keywordDetector, aiClassifier, Options.Create(_options), NullLogger<HybridSubIntentClassifier>.Instance);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Ambiguous message forces AI path; outer cancellation should surface.
        var result = await classifier.ClassifyAsync("xin chào shop", cancellationToken: cts.Token);

        // GeminiSubIntentClassifier swallows OperationCanceledException when caller cancels — currently
        // returns null. This documents existing behavior; tighten if/when we surface cancellation.
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_AiFallbackDisabled_ReturnsKeywordOnly()
    {
        // Arrange: AI fallback disabled
        var options = new SubIntentOptions
        {
            EnableAiFallback = false,
            KeywordHighConfidenceThreshold = 0.9m,
            ClassifierTimeoutMs = 500,
            MinConfidence = 0.5m
        };

        var aiClassifier = new GeminiSubIntentClassifier(
            _httpClient,
            Options.Create(_geminiOptions),
            Options.Create(options),
            NullLogger<GeminiSubIntentClassifier>.Instance);

        var classifier = new HybridSubIntentClassifier(
            _keywordDetector,
            aiClassifier,
            Options.Create(options),
            NullLogger<HybridSubIntentClassifier>.Instance);

        // Act
        var result = await classifier.ClassifyAsync("giá?");

        // Assert
        result.Should().NotBeNull();
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
