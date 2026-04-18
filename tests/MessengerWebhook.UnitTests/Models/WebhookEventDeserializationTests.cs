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
        var validWebhookEvent = webhookEvent!;
        validWebhookEvent.Object.Should().Be("page");
        validWebhookEvent.Entry.Should().HaveCount(1);

        var entry = validWebhookEvent.Entry[0];
        entry.Id.Should().Be("123456789");
        entry.Time.Should().Be(1458692752478);
        entry.Messaging.Should().HaveCount(1);

        var messagingEvent = entry.Messaging![0];
        messagingEvent.Sender.Should().NotBeNull();
        var sender = messagingEvent.Sender!;
        sender.Id.Should().Be("USER_ID");

        messagingEvent.Recipient.Should().NotBeNull();
        var recipient = messagingEvent.Recipient!;
        recipient.Id.Should().Be("PAGE_ID");

        messagingEvent.Timestamp.Should().Be(1458692752478);
        messagingEvent.Message.Should().NotBeNull();
        var message = messagingEvent.Message!;
        message.Mid.Should().Be("mid.1457764197618:41d102a3e1ae206a38");
        message.Text.Should().Be("hello, world!");
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
        var validWebhookEvent = webhookEvent!;
        validWebhookEvent.Object.Should().Be("page");
        validWebhookEvent.Entry.Should().HaveCount(1);

        var entry = validWebhookEvent.Entry[0];
        entry.Messaging.Should().HaveCount(1);

        var messagingEvent = entry.Messaging![0];
        messagingEvent.Postback.Should().NotBeNull();
        var postback = messagingEvent.Postback!;
        postback.Title.Should().Be("Get Started");
        postback.Payload.Should().Be("GET_STARTED_PAYLOAD");
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
        var validWebhookEvent = webhookEvent!;
        validWebhookEvent.Entry.Should().HaveCount(1);

        var entry = validWebhookEvent.Entry[0];
        entry.Messaging.Should().HaveCount(1);

        var messagingEvent = entry.Messaging![0];
        messagingEvent.Message.Should().NotBeNull();
        var message = messagingEvent.Message!;
        message.Attachments.Should().HaveCount(1);

        var attachment = message.Attachments![0];
        attachment.Type.Should().Be("image");
        attachment.Payload.Should().NotBeNull();
        var payload = attachment.Payload!;
        payload.Url.Should().Be("https://example.com/image.jpg");
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
        var validWebhookEvent = webhookEvent!;
        validWebhookEvent.Entry.Should().HaveCount(2);

        var firstEntry = validWebhookEvent.Entry[0];
        firstEntry.Messaging.Should().HaveCount(1);
        var firstMessage = firstEntry.Messaging![0].Message;
        firstMessage.Should().NotBeNull();
        firstMessage!.Text.Should().Be("Message 1");

        var secondEntry = validWebhookEvent.Entry[1];
        secondEntry.Messaging.Should().HaveCount(1);
        var secondMessage = secondEntry.Messaging![0].Message;
        secondMessage.Should().NotBeNull();
        secondMessage!.Text.Should().Be("Message 2");
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
        var validWebhookEvent = webhookEvent!;
        validWebhookEvent.Entry.Should().HaveCount(1);

        var entry = validWebhookEvent.Entry[0];
        entry.Messaging.Should().HaveCount(2);

        var firstSender = entry.Messaging![0].Sender;
        firstSender.Should().NotBeNull();
        firstSender!.Id.Should().Be("USER_1");

        var secondSender = entry.Messaging[1].Sender;
        secondSender.Should().NotBeNull();
        secondSender!.Id.Should().Be("USER_2");
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
        var validWebhookEvent = webhookEvent!;
        validWebhookEvent.Entry.Should().HaveCount(1);
        validWebhookEvent.Entry[0].Messaging.Should().BeEmpty();
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
