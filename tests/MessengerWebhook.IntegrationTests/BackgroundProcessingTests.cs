using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace MessengerWebhook.IntegrationTests;

public class BackgroundProcessingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public BackgroundProcessingTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task BackgroundService_ProcessesQueuedEvents_Successfully()
    {
        var response = await PostWebhookAsync("sender-processing-1", "mid.processing.1", "Tôi muốn mua kem chống nắng");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-processing-1"));

        var reply = _factory.MessengerSpy.Messages.Last(x => x.RecipientId == "sender-processing-1").Text;
        Assert.True(
            reply.Contains("Kem Chống Nắng", StringComparison.OrdinalIgnoreCase) ||
            reply.Contains("Kem Chong Nang", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BackgroundService_HandlesIdempotency_SkipsDuplicates()
    {
        var response1 = await PostWebhookAsync("sender-duplicate-1", "mid.duplicate.1", "Tôi muốn mua kem chống nắng");
        var response2 = await PostWebhookAsync("sender-duplicate-1", "mid.duplicate.1", "Tôi muốn mua kem chống nắng");

        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-duplicate-1"));

        _factory.MessengerSpy.Messages.Count(x => x.RecipientId == "sender-duplicate-1").Should().Be(1);
        _output.WriteLine("Duplicate webhook message was only processed once.");
    }

    [Fact]
    public async Task BackgroundService_RespectsBotLock_AndSkipsReply()
    {
        var response = await PostWebhookAsync("psid-case-primary", "mid.locked.1", "Tôi muốn mua kem chống nắng");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await Task.Delay(500);
        _factory.MessengerSpy.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task BackgroundService_ProcessesPostback_Successfully()
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender-postback-1" },
                            recipient = new { id = _factory.PrimaryPageId },
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

        var response = await PostSignedJsonAsync(JsonSerializer.Serialize(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-postback-1"));
        _factory.MessengerSpy.Messages.Last(x => x.RecipientId == "sender-postback-1").Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task BackgroundService_ProcessingLatency_UnderFiveSeconds()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await PostWebhookAsync("sender-latency-1", "mid.latency.1", "Tôi muốn mua kem lụa");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-latency-1"));
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        _output.WriteLine($"Processing latency: {stopwatch.Elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task BackgroundService_CreatesDraftAfterCollectingContactInfo_AndExplicitBuyIntent()
    {
        await PostWebhookAsync("sender-draft-1", "mid.draft.1", "Tôi muốn mua kem chống nắng");
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-draft-1"));
        _factory.MessengerSpy.Clear();

        var contactResponse = await PostWebhookAsync(
            "sender-draft-1",
            "mid.draft.2",
            "Số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1");

        contactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-draft-1"));
        _factory.MessengerSpy.Clear();

        var buyResponse = await PostWebhookAsync(
            "sender-draft-1",
            "mid.draft.3",
            "ok em lên đơn nhé");

        buyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-draft-1"));
        _factory.MessengerSpy.Clear();

        var confirmResponse = await PostWebhookAsync(
            "sender-draft-1",
            "mid.draft.4",
            "đúng rồi");

        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "sender-draft-1"));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var draft = dbContext.DraftOrders.Single(x => x.FacebookPSID == "sender-draft-1");
        draft.CustomerPhone.Should().Be("0901234567");
        draft.ShippingAddress.Should().Contain("12 Trần Hưng Đạo");
        draft.PriceConfirmed.Should().BeTrue();
        draft.ShippingConfirmed.Should().BeFalse();

        var customer = dbContext.CustomerIdentities.Single(x => x.FacebookPSID == "sender-draft-1");
        customer.TotalOrders.Should().Be(0);
        customer.LifetimeValue.Should().Be(0);
    }

    private Task<HttpResponseMessage> PostWebhookAsync(string senderId, string messageId, string text)
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = senderId },
                            recipient = new { id = _factory.PrimaryPageId },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            message = new
                            {
                                mid = messageId,
                                text
                            }
                        }
                    }
                }
            }
        };

        return PostSignedJsonAsync(JsonSerializer.Serialize(payload));
    }

    private async Task<HttpResponseMessage> PostSignedJsonAsync(string json)
    {
        var signature = ComputeSignature(json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);
        return await _client.PostAsync("/webhook", content);
    }

    private string ComputeSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 4000)
    {
        var started = Stopwatch.StartNew();
        while (started.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        condition().Should().BeTrue("the background worker should finish inside the timeout");
    }
}
