using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Models;

namespace MessengerWebhook.UnitTests.Models;

/// <summary>
/// Unit tests for WebhookEvent JSON deserialization
/// </summary>
public class WebhookEventDeserializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Deserialize_ValidMessageEvent_Success()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [{
                "id": "123456789",
                "time": 1458692752478,
                "messaging": [{
                    "sender": {"id": "USER_ID"},
                    "recipient": {"id": "PAGE_ID"},
                    "timestamp": 1458692752478,
                    "message": {
                        "mid": "mid.1457764197618:41d102a3e1ae206a38",
                        "text": "hello, world!"
                    }
                }]
            }]
        }
        """;

        // Act
        var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);

        // Assert
        webhookEvent.Should().NotBeNull();
        webhookEvent!.Object.Should().Be("page");
        webhookEvent.Entry.Should().HaveCount(1);

        var entry = webhookEvent.Entry[0];
        entry.Id.Should().Be("123456789");
        entry.Time.Should().Be(1458692752478);
        entry.Messaging.Should().HaveCount(1);

        var messagingEvent = entry.Messaging[0];
        messagingEvent.Sender.Id.Should().Be("USER_ID");
        messagingEvent.Recipient.Id.Should().Be("PAGE_ID");
        messagingEvent.Timestamp.Should().Be(1458692752478);
        messagingEvent.Message.Should().NotBeNull();
        messagingEvent.Message!.Mid.Should().Be("mid.1457764197618:41d102a3e1ae206a38");
        messagingEvent.Message.Text.Should().Be("hello, world!");
        messagingEvent.Postback.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ValidPostbackEvent_Success()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [{
                "id": "123456789",
                "time": 1458692752478,
                "messaging": [{
                    "sender": {"id": "USER_ID"},
                    "recipient": {"id": "PAGE_ID"},
                    "timestamp": 1458692752478,
                    "postback": {
                        "title": "Get Started",
                        "payload": "GET_STARTED_PAYLOAD"
                    }
                }]
            }]
        }
        """;

        // Act
        var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);

        // Assert
        webhookEvent.Should().NotBeNull();
        webhookEvent!.Object.Should().Be("page");

        var messagingEvent = webhookEvent.Entry[0].Messaging[0];
        messagingEvent.Postback.Should().NotBeNull();
        messagingEvent.Postback!.Title.Should().Be("Get Started");
        messagingEvent.Postback.Payload.Should().Be("GET_STARTED_PAYLOAD");
        messagingEvent.Message.Should().BeNull();
    }

    [Fact]
    public void Deserialize_MessageWithAttachments_Success()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [{
                "id": "123456789",
                "time": 1458692752478,
                "messaging": [{
                    "sender": {"id": "USER_ID"},
                    "recipient": {"id": "PAGE_ID"},
                    "timestamp": 1458692752478,
                    "message": {
                        "mid": "mid.1458696618141:b4ef9d19ec21086067",
                        "attachments": [{
                            "type": "image",
                            "payload": {
                                "url": "https://example.com/image.jpg"
                            }
                        }]
                    }
                }]
            }]
        }
        """;

        // Act
        var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);

        // Assert
        webhookEvent.Should().NotBeNull();
        var message = webhookEvent!.Entry[0].Messaging[0].Message;
        message.Should().NotBeNull();
        message!.Attachments.Should().HaveCount(1);
        message.Attachments![0].Type.Should().Be("image");
        message.Attachments[0].Payload.Should().NotBeNull();
        message.Attachments[0].Payload!.Url.Should().Be("https://example.com/image.jpg");
    }

    [Fact]
    public void Deserialize_MultipleEntries_Success()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [
                {
                    "id": "123456789",
                    "time": 1458692752478,
                    "messaging": [{
                        "sender": {"id": "USER_1"},
                        "recipient": {"id": "PAGE_ID"},
                        "timestamp": 1458692752478,
                        "message": {
                            "mid": "mid.1",
                            "text": "Message 1"
                        }
                    }]
                },
                {
                    "id": "987654321",
                    "time": 1458692752479,
                    "messaging": [{
                        "sender": {"id": "USER_2"},
                        "recipient": {"id": "PAGE_ID"},
                        "timestamp": 1458692752479,
                        "message": {
                            "mid": "mid.2",
                            "text": "Message 2"
                        }
                    }]
                }
            ]
        }
        """;

        // Act
        var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);

        // Assert
        webhookEvent.Should().NotBeNull();
        webhookEvent!.Entry.Should().HaveCount(2);
        webhookEvent.Entry[0].Messaging[0].Message!.Text.Should().Be("Message 1");
        webhookEvent.Entry[1].Messaging[0].Message!.Text.Should().Be("Message 2");
    }

    [Fact]
    public void Deserialize_MultipleMessagingEventsInEntry_Success()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [{
                "id": "123456789",
                "time": 1458692752478,
                "messaging": [
                    {
                        "sender": {"id": "USER_1"},
                        "recipient": {"id": "PAGE_ID"},
                        "timestamp": 1458692752478,
                        "message": {
                            "mid": "mid.1",
                            "text": "First message"
                        }
                    },
                    {
                        "sender": {"id": "USER_2"},
                        "recipient": {"id": "PAGE_ID"},
                        "timestamp": 1458692752479,
                        "message": {
                            "mid": "mid.2",
                            "text": "Second message"
                        }
                    }
                ]
            }]
        }
        """;

        // Act
        var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);

        // Assert
        webhookEvent.Should().NotBeNull();
        webhookEvent!.Entry[0].Messaging.Should().HaveCount(2);
        webhookEvent.Entry[0].Messaging[0].Sender.Id.Should().Be("USER_1");
        webhookEvent.Entry[0].Messaging[1].Sender.Id.Should().Be("USER_2");
    }

    [Fact]
    public void Deserialize_EmptyMessagingArray_Success()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [{
                "id": "123456789",
                "time": 1458692752478,
                "messaging": []
            }]
        }
        """;

        // Act
        var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);

        // Assert
        webhookEvent.Should().NotBeNull();
        webhookEvent!.Entry[0].Messaging.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsException()
    {
        // Arrange
        var json = """
        {
            "object": "page",
            "entry": [{
                "id": "123456789"
        """;

        // Act & Assert
        var act = () => JsonSerializer.Deserialize<WebhookEvent>(json, _jsonOptions);
        act.Should().Throw<JsonException>();
    }
}
