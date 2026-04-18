using System.Net;
using System.Text.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.AI.Strategies;
using MessengerWebhook.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace MessengerWebhook.UnitTests.Services.AI;

public class GeminiServiceTests
{
    private readonly Mock<IModelSelectionStrategy> _strategyMock;
    private readonly Mock<ILogger<GeminiService>> _loggerMock;
    private readonly GeminiOptions _options;

    public GeminiServiceTests()
    {
        _strategyMock = new Mock<IModelSelectionStrategy>();
        _loggerMock = new Mock<ILogger<GeminiService>>();
        _options = new GeminiOptions
        {
            ApiKey = "test-api-key",
            ProModel = "gemini-1.5-pro",
            FlashLiteModel = "gemini-1.5-flash",
            MaxTokens = 2048,
            Temperature = 0.7,
            SystemPromptPath = "prompts/system.txt"
        };
    }

    [Fact]
    public async Task SendMessageAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = "AI response here" } }
                    },
                    finishReason = "STOP"
                }
            },
            usageMetadata = new { totalTokenCount = 100 }
        });

        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(responseJson);
        var httpClient = new HttpClient(mockHandler);

        _strategyMock.Setup(x => x.SelectModel(It.IsAny<string>()))
            .Returns(GeminiModelType.FlashLite);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.SendMessageAsync(
            "user123",
            "Hello",
            new List<ConversationMessage>());

        // Assert
        result.Should().Be("AI response here");
    }

    [Fact]
    public async Task SendMessageAsync_WithHistory_IncludesHistoryInRequest()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new { parts = new[] { new { text = "Response" } } },
                    finishReason = "STOP"
                }
            }
        });

        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(responseJson);
        var httpClient = new HttpClient(mockHandler);

        _strategyMock.Setup(x => x.SelectModel(It.IsAny<string>()))
            .Returns(GeminiModelType.Pro);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Previous message" },
            new() { Role = "model", Content = "Previous response" }
        };

        // Act
        var result = await service.SendMessageAsync("user123", "New message", history);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendMessageAsync_EmptyHistory_AddsSystemPrompt()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new { parts = new[] { new { text = "Response" } } },
                    finishReason = "STOP"
                }
            }
        });

        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(responseJson);
        var httpClient = new HttpClient(mockHandler);

        _strategyMock.Setup(x => x.SelectModel(It.IsAny<string>()))
            .Returns(GeminiModelType.FlashLite);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.SendMessageAsync(
            "user123",
            "Hello",
            new List<ConversationMessage>());

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendMessageAsync_ApiError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(
            "{\"error\": \"API error\"}",
            HttpStatusCode.BadRequest);

        var httpClient = new HttpClient(mockHandler);

        _strategyMock.Setup(x => x.SelectModel(It.IsAny<string>()))
            .Returns(GeminiModelType.FlashLite);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendMessageAsync("user123", "Hello", new List<ConversationMessage>()));
    }

    [Fact]
    public async Task SendMessageAsync_NoCandidates_ThrowsInvalidOperationException()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new { candidates = Array.Empty<object>() });

        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse(responseJson);
        var httpClient = new HttpClient(mockHandler);

        _strategyMock.Setup(x => x.SelectModel(It.IsAny<string>()))
            .Returns(GeminiModelType.FlashLite);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendMessageAsync("user123", "Hello", new List<ConversationMessage>()));
    }

    [Fact]
    public async Task SendMessageAsync_MessageTooLong_ThrowsArgumentException()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var httpClient = new HttpClient(mockHandler);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        var longMessage = new string('a', 10001);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendMessageAsync("user123", longMessage, new List<ConversationMessage>()));
    }

    [Fact]
    public async Task SendMessageAsync_NullUserId_ThrowsArgumentException()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var httpClient = new HttpClient(mockHandler);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SendMessageAsync(null!, "Hello", new List<ConversationMessage>()));
    }

    [Fact]
    public async Task SendMessageAsync_NullMessage_ThrowsArgumentException()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var httpClient = new HttpClient(mockHandler);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SendMessageAsync("user123", null!, new List<ConversationMessage>()));
    }

    [Fact]
    public async Task DetectConfirmationAsync_RequestShouldUseMaskedBooleanContext()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var responseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new { parts = new[] { new { text = "{\"IsConfirming\":true,\"Confidence\":0.9,\"Reason\":\"ok\"}" } } },
                    finishReason = "STOP"
                }
            }
        });

        var mockHandler = new DelegatingCaptureHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        });
        var httpClient = new HttpClient(mockHandler);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.DetectConfirmationAsync(
            "nhu cu",
            "0912345678",
            "123 Nguyen Trai Quan 1 TPHCM");

        // Assert
        result.IsConfirming.Should().BeTrue();
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("hasPhone=True");
        body.Should().Contain("hasAddress=True");
        body.Should().Contain("091****678");
        body.Should().NotContain("0912345678");
        body.Should().NotContain("123 Nguyen Trai Quan 1 TPHCM");
    }

    [Fact]
    public void SelectModel_DelegatesToStrategy()
    {
        // Arrange
        var mockHandler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var httpClient = new HttpClient(mockHandler);

        _strategyMock.Setup(x => x.SelectModel("test message"))
            .Returns(GeminiModelType.Pro);

        var service = new GeminiService(
            httpClient,
            Options.Create(_options),
            _strategyMock.Object,
            _loggerMock.Object);

        // Act
        var result = service.SelectModel("test message");

        // Assert
        result.Should().Be(GeminiModelType.Pro);
        _strategyMock.Verify(x => x.SelectModel("test message"), Times.Once);
    }

    private sealed class DelegatingCaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public DelegatingCaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
