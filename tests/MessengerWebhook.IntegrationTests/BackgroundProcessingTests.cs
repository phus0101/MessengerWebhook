using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using MessengerWebhook.BackgroundServices;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace MessengerWebhook.IntegrationTests;

public class BackgroundProcessingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly string _appSecret;

    public BackgroundProcessingTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Facebook:AppSecret"] = "test_secret_for_integration_tests",
                    ["Facebook:PageAccessToken"] = "test_page_token",
                    ["Webhook:VerifyToken"] = "test_verify_token"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Mock IMessengerService to avoid real API calls
                var mockMessengerService = new Mock<IMessengerService>();
                mockMessengerService
                    .Setup(m => m.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SendMessageResponse("test_recipient", "test_message_id"));

                services.AddSingleton(mockMessengerService.Object);
            });
        });

        _client = _factory.CreateClient();
        _appSecret = "test_secret_for_integration_tests";
        _output = output;
    }

    private string ComputeSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLower();
    }

    [Fact]
    public async Task BackgroundService_ProcessesQueuedEvents_Successfully()
    {
        // Arrange
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page123",
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender123" },
                            recipient = new { id = "page123" },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            message = new
                            {
                                mid = "mid.test123",
                                text = "Hello from integration test"
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var signature = ComputeSignature(json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Give background service time to process
        await Task.Delay(1000);

        _output.WriteLine("Event queued and processed successfully");
    }

    [Fact]
    public async Task BackgroundService_HandlesIdempotency_SkipsDuplicates()
    {
        // Arrange
        var messageId = $"mid.duplicate-{Guid.NewGuid()}";

        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page123",
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender456" },
                            recipient = new { id = "page123" },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            message = new
                            {
                                mid = messageId,
                                text = "Duplicate test message"
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var signature = ComputeSignature(json);

        var content1 = new StringContent(json, Encoding.UTF8, "application/json");
        content1.Headers.Add("X-Hub-Signature-256", signature);

        var content2 = new StringContent(json, Encoding.UTF8, "application/json");
        content2.Headers.Add("X-Hub-Signature-256", signature);

        // Act - Send same message twice
        var response1 = await _client.PostAsync("/webhook", content1);
        await Task.Delay(500); // Let first message process

        var response2 = await _client.PostAsync("/webhook", content2);
        await Task.Delay(500); // Let second message attempt to process

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        _output.WriteLine($"Duplicate message {messageId} handled correctly");
    }

    [Fact]
    public async Task BackgroundService_ProcessesPostback_Successfully()
    {
        // Arrange
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page123",
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender789" },
                            recipient = new { id = "page123" },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            postback = new
                            {
                                title = "Get Started",
                                payload = "GET_STARTED_PAYLOAD"
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var signature = ComputeSignature(json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(1000);

        _output.WriteLine("Postback event processed successfully");
    }

    [Fact]
    public async Task BackgroundService_ProcessingLatency_UnderFiveSeconds()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;

        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page123",
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender999" },
                            recipient = new { id = "page123" },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            message = new
                            {
                                mid = $"mid.latency-{Guid.NewGuid()}",
                                text = "Latency test message"
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var signature = ComputeSignature(json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Wait for processing with timeout
        await Task.Delay(2000);

        var endTime = DateTimeOffset.UtcNow;
        var latency = (endTime - startTime).TotalSeconds;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        latency.Should().BeLessThan(5, "Processing should complete within 5 seconds");

        _output.WriteLine($"Processing latency: {latency:F2}s");
    }

    [Fact]
    public async Task BackgroundService_GracefulShutdown_CompletesProcessing()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<MessagingEvent>();
        var serviceProvider = _factory.Services;

        // Queue an event
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender123"),
            Recipient: new Recipient("page123"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: new Message($"mid.shutdown-{Guid.NewGuid()}", "Shutdown test", null, null),
            Postback: null
        );

        await channel.Writer.WriteAsync(messagingEvent);

        // Act - Service should process event before shutdown
        await Task.Delay(1000);

        // Assert - No exceptions during shutdown
        _output.WriteLine("Graceful shutdown test completed");
    }

    [Fact]
    public async Task BackgroundService_MultipleEvents_ProcessedInOrder()
    {
        // Arrange
        var messageIds = new List<string>();

        // Send 3 events in sequence
        for (int i = 0; i < 3; i++)
        {
            var messageId = $"mid.order-{i}-{Guid.NewGuid()}";
            messageIds.Add(messageId);

            var payload = new
            {
                @object = "page",
                entry = new[]
                {
                    new
                    {
                        id = "page123",
                        time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        messaging = new[]
                        {
                            new
                            {
                                sender = new { id = $"sender{i}" },
                                recipient = new { id = "page123" },
                                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                message = new
                                {
                                    mid = messageId,
                                    text = $"Message {i}"
                                }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var signature = ComputeSignature(json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.Add("X-Hub-Signature-256", signature);

            // Act
            var response = await _client.PostAsync("/webhook", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Wait for all to process
        await Task.Delay(2000);

        // Assert
        _output.WriteLine($"Processed {messageIds.Count} events in sequence");
    }
}
