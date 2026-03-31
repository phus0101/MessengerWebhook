using System.Net;
using System.Text.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
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

public class GeminiIntentDetectionTests
{
    private readonly Mock<IModelSelectionStrategy> _strategyMock;
    private readonly Mock<ILogger<GeminiService>> _loggerMock;
    private readonly GeminiOptions _options;

    public GeminiIntentDetectionTests()
    {
        _strategyMock = new Mock<IModelSelectionStrategy>();
        _loggerMock = new Mock<ILogger<GeminiService>>();
        _options = new GeminiOptions
        {
            ApiKey = "test-api-key",
            FlashLiteModel = "gemini-1.5-flash",
            EnableAiIntentDetection = true,
            IntentConfidenceThreshold = 0.7
        };
    }

    [Theory]
    [InlineData("cần tư vấn thêm", CustomerIntent.Consulting)]
    [InlineData("cho em hỏi về sản phẩm", CustomerIntent.Consulting)]
    [InlineData("tư vấn giúp em", CustomerIntent.Consulting)]
    public async Task DetectIntentAsync_ConsultingMessages_ReturnsConsultingIntent(string message, CustomerIntent expectedIntent)
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
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    intent = "Consulting",
                                    confidence = 0.95,
                                    reason = "Customer explicitly asks for consultation"
                                })
                            }
                        }
                    }
                }
            }
        });

        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.OK, responseJson);
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            message,
            ConversationState.Consulting,
            hasProduct: false,
            hasContact: false);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(expectedIntent);
        result.Confidence.Should().BeGreaterThan(0.7);
        result.DetectionMethod.Should().Be("ai-reasoning");
    }

    [Theory]
    [InlineData("đặt hàng luôn", CustomerIntent.ReadyToBuy)]
    [InlineData("chốt đơn nha", CustomerIntent.ReadyToBuy)]
    [InlineData("lên đơn luôn em", CustomerIntent.ReadyToBuy)]
    [InlineData("mua luôn", CustomerIntent.ReadyToBuy)]
    public async Task DetectIntentAsync_ReadyToBuyMessages_ReturnsReadyToBuyIntent(string message, CustomerIntent expectedIntent)
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
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    intent = "ReadyToBuy",
                                    confidence = 0.98,
                                    reason = "Customer explicitly wants to place order"
                                })
                            }
                        }
                    }
                }
            }
        });

        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.OK, responseJson);
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            message,
            ConversationState.CollectingInfo,
            hasProduct: true,
            hasContact: true);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(expectedIntent);
        result.Confidence.Should().BeGreaterThan(0.7);
        result.DetectionMethod.Should().Be("ai-reasoning");
    }

    [Theory]
    [InlineData("tính xem", CustomerIntent.Browsing)]
    [InlineData("xem thử sản phẩm", CustomerIntent.Browsing)]
    [InlineData("có sản phẩm gì", CustomerIntent.Browsing)]
    public async Task DetectIntentAsync_BrowsingMessages_ReturnsBrowsingIntent(string message, CustomerIntent expectedIntent)
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
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    intent = "Browsing",
                                    confidence = 0.85,
                                    reason = "Customer is exploring options"
                                })
                            }
                        }
                    }
                }
            }
        });

        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.OK, responseJson);
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            message,
            ConversationState.Consulting,
            hasProduct: false,
            hasContact: false);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(expectedIntent);
        result.Confidence.Should().BeGreaterThan(0.7);
        result.DetectionMethod.Should().Be("ai-reasoning");
    }

    [Theory]
    [InlineData("đúng rồi", CustomerIntent.Confirming)]
    [InlineData("ok em", CustomerIntent.Confirming)]
    [InlineData("vâng ạ", CustomerIntent.Confirming)]
    public async Task DetectIntentAsync_ConfirmingMessages_ReturnsConfirmingIntent(string message, CustomerIntent expectedIntent)
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
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    intent = "Confirming",
                                    confidence = 0.92,
                                    reason = "Customer is confirming previous information"
                                })
                            }
                        }
                    }
                }
            }
        });

        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.OK, responseJson);
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            message,
            ConversationState.CollectingInfo,
            hasProduct: true,
            hasContact: false);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(expectedIntent);
        result.Confidence.Should().BeGreaterThan(0.7);
        result.DetectionMethod.Should().Be("ai-reasoning");
    }

    [Theory]
    [InlineData("ship bao lâu?", CustomerIntent.Questioning)]
    [InlineData("giá bao nhiêu?", CustomerIntent.Questioning)]
    [InlineData("có freeship không?", CustomerIntent.Questioning)]
    public async Task DetectIntentAsync_QuestioningMessages_ReturnsQuestioningIntent(string message, CustomerIntent expectedIntent)
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
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    intent = "Questioning",
                                    confidence = 0.88,
                                    reason = "Customer is asking questions"
                                })
                            }
                        }
                    }
                }
            }
        });

        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.OK, responseJson);
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            message,
            ConversationState.Consulting,
            hasProduct: true,
            hasContact: false);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(expectedIntent);
        result.Confidence.Should().BeGreaterThan(0.7);
        result.DetectionMethod.Should().Be("ai-reasoning");
    }

    [Fact]
    public async Task DetectIntentAsync_WhenFeatureDisabled_ReturnsFallbackConsulting()
    {
        // Arrange
        var disabledOptions = new GeminiOptions
        {
            ApiKey = "test-api-key",
            FlashLiteModel = "gemini-1.5-flash",
            EnableAiIntentDetection = false
        };

        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.OK, "{}");
        var service = new GeminiService(httpClient, Options.Create(disabledOptions), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            "đặt hàng luôn",
            ConversationState.Consulting,
            hasProduct: true,
            hasContact: true);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(CustomerIntent.Consulting);
        result.Confidence.Should().Be(0.0);
        result.DetectionMethod.Should().Be("fallback");
        result.Reason.Should().Contain("disabled");
    }

    [Fact]
    public async Task DetectIntentAsync_WhenApiError_ReturnsFallbackConsulting()
    {
        // Arrange
        var httpClient = MockHttpClientFactory.CreateWithResponse(HttpStatusCode.InternalServerError, "API Error");
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            "cần tư vấn",
            ConversationState.Consulting,
            hasProduct: false,
            hasContact: false);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(CustomerIntent.Consulting);
        result.Confidence.Should().Be(0.0);
        result.DetectionMethod.Should().Be("fallback");
        result.Reason.Should().Contain("API error");
    }

    [Fact]
    public async Task DetectIntentAsync_WhenTimeout_ReturnsFallbackConsulting()
    {
        // Arrange
        var httpClient = MockHttpClientFactory.CreateWithDelay(TimeSpan.FromSeconds(2));
        var service = new GeminiService(httpClient, Options.Create(_options), _strategyMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectIntentAsync(
            "cần tư vấn",
            ConversationState.Consulting,
            hasProduct: false,
            hasContact: false);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(CustomerIntent.Consulting);
        result.Confidence.Should().Be(0.0);
        result.DetectionMethod.Should().Be("fallback");
        result.Reason.Should().Contain("Timeout");
    }
}
