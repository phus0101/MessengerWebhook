using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace MessengerWebhook.IntegrationTests;

public class WebhookEventEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public WebhookEventEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWebhook_ValidMessageEvent_Returns200AndProcessesSalesReply()
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = 1458692752478L,
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "USER_MESSAGE_1" },
                            recipient = new { id = _factory.PrimaryPageId },
                            timestamp = 1458692752478L,
                            message = new
                            {
                                mid = "mid.1457764197618:41d102a3e1ae206a38",
                                text = "Tôi muốn mua kem chống nắng"
                            }
                        }
                    }
                }
            }
        };

        var response = await PostWithSignatureAsync(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("status").GetString().Should().Be("EVENT_RECEIVED");

        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "USER_MESSAGE_1"));
        var sentMessage = _factory.MessengerSpy.Messages.Last(x => x.RecipientId == "USER_MESSAGE_1");
        Assert.True(
            sentMessage.Text.Contains("Kem Chống Nắng", StringComparison.OrdinalIgnoreCase) ||
            sentMessage.Text.Contains("Kem Chong Nang", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostWebhook_ValidPostbackEvent_Returns200AndProcessesFallbackReply()
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = 1458692752478L,
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "USER_POSTBACK_1" },
                            recipient = new { id = _factory.PrimaryPageId },
                            timestamp = 1458692752478L,
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

        var response = await PostWithSignatureAsync(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() => _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "USER_POSTBACK_1"));
        _factory.MessengerSpy.Messages.Last(x => x.RecipientId == "USER_POSTBACK_1").Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostWebhook_MultipleEvents_Returns200AndProcessesAllMessages()
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = 1458692752478L,
                    messaging = new object[]
                    {
                        new
                        {
                            sender = new { id = "USER_MULTI_1" },
                            recipient = new { id = _factory.PrimaryPageId },
                            timestamp = 1458692752478L,
                            message = new
                            {
                                mid = "mid.multi.1",
                                text = "Tôi muốn mua kem chống nắng"
                            }
                        },
                        new
                        {
                            sender = new { id = "USER_MULTI_2" },
                            recipient = new { id = _factory.PrimaryPageId },
                            timestamp = 1458692752479L,
                            message = new
                            {
                                mid = "mid.multi.2",
                                text = "Tôi muốn mua kem lụa"
                            }
                        }
                    }
                }
            }
        };

        var response = await PostWithSignatureAsync(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForAsync(() =>
            _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "USER_MULTI_1") &&
            _factory.MessengerSpy.Messages.Any(x => x.RecipientId == "USER_MULTI_2"));
        _factory.MessengerSpy.Messages.Select(x => x.RecipientId).Should().Contain(new[] { "USER_MULTI_1", "USER_MULTI_2" });
    }

    [Fact]
    public async Task PostWebhook_InvalidObjectType_Returns404()
    {
        var payload = new
        {
            @object = "user",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = 1458692752478L,
                    messaging = Array.Empty<object>()
                }
            }
        };

        var response = await PostWithSignatureAsync(payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostWebhook_MalformedJson_Returns400()
    {
        const string malformedJson = """
        {
            "object": "page",
            "entry": [{
                "id": "PAGE_TEST_1"
        """;

        var response = await PostRawWithSignatureAsync(malformedJson);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWebhook_EmptyMessagingArray_Returns200WithoutSendingReply()
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = 1458692752478L,
                    messaging = Array.Empty<object>()
                }
            }
        };

        var response = await PostWithSignatureAsync(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await Task.Delay(300);
        _factory.MessengerSpy.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task PostWebhook_ResponseTime_ShouldStayFast()
    {
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = _factory.PrimaryPageId,
                    time = 1458692752478L,
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "USER_PERF_1" },
                            recipient = new { id = _factory.PrimaryPageId },
                            timestamp = 1458692752478L,
                            message = new
                            {
                                mid = "mid.perf.1",
                                text = "Tôi muốn mua kem chống nắng"
                            }
                        }
                    }
                }
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var response = await PostWithSignatureAsync(payload);
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(250);
    }

    private Task<HttpResponseMessage> PostWithSignatureAsync(object payload)
    {
        return PostRawWithSignatureAsync(JsonSerializer.Serialize(payload));
    }

    private async Task<HttpResponseMessage> PostRawWithSignatureAsync(string rawJson)
    {
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", ComputeSignature(rawJson));
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
