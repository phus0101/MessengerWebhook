using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessengerWebhook.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests;

public class LiveCommentWebhookTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LiveCommentWebhookTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWebhook_WithFeedCommentEvent_ProcessesSuccessfully()
    {
        var webhookEvent = new WebhookEvent(
            "page",
            new[]
            {
                new Entry(
                    "page123",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    null,
                    new[]
                    {
                        new FeedChange
                        {
                            Field = "feed",
                            Value = new FeedChangeValue
                            {
                                Item = "comment",
                                CommentId = "comment123",
                                PostId = "video456_789",
                                From = new FeedUser { Id = "user123", Name = "Test User" },
                                Message = "Mua hàng"
                            }
                        }
                    })
            });

        var response = await PostWithSignatureAsync(webhookEvent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EVENT_RECEIVED", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostWebhook_WithNonLiveVideoComment_IgnoresComment()
    {
        var webhookEvent = new WebhookEvent(
            "page",
            new[]
            {
                new Entry(
                    "page123",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    null,
                    new[]
                    {
                        new FeedChange
                        {
                            Field = "feed",
                            Value = new FeedChangeValue
                            {
                                Item = "comment",
                                CommentId = "comment456",
                                PostId = "regular_post_789",
                                From = new FeedUser { Id = "user456", Name = "Another User" },
                                Message = "Nice post"
                            }
                        }
                    })
            });

        var response = await PostWithSignatureAsync(webhookEvent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithMultipleFeedChanges_ProcessesAll()
    {
        var webhookEvent = new WebhookEvent(
            "page",
            new[]
            {
                new Entry(
                    "page123",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    null,
                    new[]
                    {
                        new FeedChange
                        {
                            Field = "feed",
                            Value = new FeedChangeValue
                            {
                                Item = "comment",
                                CommentId = "comment1",
                                PostId = "video456_789",
                                From = new FeedUser { Id = "user1", Name = "User 1" },
                                Message = "Đặt hàng"
                            }
                        },
                        new FeedChange
                        {
                            Field = "feed",
                            Value = new FeedChangeValue
                            {
                                Item = "comment",
                                CommentId = "comment2",
                                PostId = "video456_789",
                                From = new FeedUser { Id = "user2", Name = "User 2" },
                                Message = "Mua"
                            }
                        }
                    })
            });

        var response = await PostWithSignatureAsync(webhookEvent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithBothMessagingAndFeedEvents_ProcessesBoth()
    {
        var webhookEvent = new WebhookEvent(
            "page",
            new[]
            {
                new Entry(
                    "page123",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new[]
                    {
                        new MessagingEvent(
                            new Sender("sender123"),
                            new Recipient("page123"),
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            new Message("mid.123", "Hello", null, null),
                            null)
                    },
                    new[]
                    {
                        new FeedChange
                        {
                            Field = "feed",
                            Value = new FeedChangeValue
                            {
                                Item = "comment",
                                CommentId = "comment789",
                                PostId = "video456_789",
                                From = new FeedUser { Id = "user789", Name = "User 789" },
                                Message = "Order"
                            }
                        }
                    })
            });

        var response = await PostWithSignatureAsync(webhookEvent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostWithSignatureAsync(WebhookEvent webhookEvent)
    {
        var rawJson = JsonSerializer.Serialize(webhookEvent);
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", ComputeSignature(rawJson));
        return _client.PostAsync("/webhook", content);
    }

    private string ComputeSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
