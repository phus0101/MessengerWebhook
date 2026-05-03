using System.Net;
using System.Text.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.UnitTests.Services.Policy;

public class GeminiPolicyIntentClassifierTests
{
    private static readonly GeminiOptions GeminiOptions = new()
    {
        FlashLiteModel = "gemini-1.5-flash"
    };

    [Fact]
    public async Task ClassifyAsync_ManualReviewResponse_ReturnsClassification()
    {
        var classifier = CreateClassifier(CreateGeminiResponse("manual_review", 0.91m, "asked for human support", ["nhan vien ho tro"]));

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.NotNull(result);
        Assert.Equal(PolicySemanticIntent.ManualReview, result!.Intent);
        Assert.Equal(SupportCaseReason.ManualReview, result.Reason);
        Assert.Equal(0.91m, result.Confidence);
        Assert.Contains("nhan vien ho tro", result.MatchedSpans);
    }

    [Fact]
    public async Task ClassifyAsync_NonManualReviewResponse_ReturnsNull()
    {
        var classifier = CreateClassifier(CreateGeminiResponse("none", 0.96m, "product question", ["gia bao nhieu"]));

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("gia bao nhieu"),
            "gia bao nhieu");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_FencedJsonResponse_ReturnsClassification()
    {
        var responseText = "```json\n" + JsonSerializer.Serialize(new
        {
            category = "manual_review",
            confidence = 0.91m,
            explanation = "asked for human support",
            matchedSpans = new[] { "nhan vien ho tro" }
        }) + "\n```";
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = responseText } }
                    }
                }
            }
        }));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.NotNull(result);
        Assert.Equal(PolicySemanticIntent.ManualReview, result!.Intent);
        Assert.Equal(SupportCaseReason.ManualReview, result.Reason);
    }

    [Fact]
    public async Task ClassifyAsync_FencedJsonWithoutLanguageTag_ReturnsClassification()
    {
        var responseText = "```\n" + JsonSerializer.Serialize(new
        {
            category = "manual_review",
            confidence = 0.91m,
            explanation = "asked for human support",
            matchedSpans = new[] { "nhan vien ho tro" }
        }) + "\n```";
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = responseText } }
                    }
                }
            }
        }));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.NotNull(result);
        Assert.Equal(PolicySemanticIntent.ManualReview, result!.Intent);
    }

    [Fact]
    public async Task ClassifyAsync_FencedJsonWithCrLf_ReturnsClassification()
    {
        var responseText = "```json\r\n" + JsonSerializer.Serialize(new
        {
            category = "manual_review",
            confidence = 0.91m,
            explanation = "asked for human support",
            matchedSpans = new[] { "nhan vien ho tro" }
        }) + "\r\n```";
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = responseText } }
                    }
                }
            }
        }));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.NotNull(result);
        Assert.Equal(PolicySemanticIntent.ManualReview, result!.Intent);
    }

    [Fact]
    public async Task ClassifyAsync_InvalidFencedJson_ReturnsNull()
    {
        var responseText = "```json\nnot-json\n```";
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = responseText } }
                    }
                }
            }
        }));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_UnterminatedFencedJson_ReturnsNull()
    {
        var responseText = "```json\n{\"category\":\"manual_review\"";
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = responseText } }
                    }
                }
            }
        }));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_SingleLineFencedJson_ReturnsClassification()
    {
        var responseText = "```json {\"category\":\"manual_review\",\"confidence\":0.91,\"explanation\":\"asked for human support\",\"matchedSpans\":[\"nhan vien ho tro\"]}```";
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = responseText } }
                    }
                }
            }
        }));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon nhan vien ho tro truong hop nay"),
            "toi muon nhan vien ho tro truong hop nay");

        Assert.NotNull(result);
        Assert.Equal(PolicySemanticIntent.ManualReview, result!.Intent);
    }

    [Fact]
    public async Task ClassifyAsync_UncertainResponse_ReturnsNull()
    {
        var classifier = CreateClassifier(CreateGeminiResponse("uncertain", 0.93m, "ambiguous request", ["xem giup"]));

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("xem giup minh"),
            "xem giup minh");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_LowConfidenceManualReview_ReturnsNull()
    {
        var classifier = CreateClassifier(CreateGeminiResponse("manual_review", 0.60m, "too weak", ["ho tro"]));

        var result = await classifier.ClassifyAsync(
            new PolicyGuardRequest("toi muon ho tro"),
            "toi muon ho tro");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_InvalidJson_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = "not-json" } }
                    }
                }
            }
        }));

        var classifier = CreateClassifier(handler);
        var result = await classifier.ClassifyAsync(new PolicyGuardRequest("toi muon nhan vien ho tro"), "toi muon nhan vien ho tro");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_ApiError_ReturnsNull()
    {
        var classifier = CreateClassifier(MockHttpMessageHandler.CreateWithError(HttpStatusCode.BadGateway, "upstream error"));

        var result = await classifier.ClassifyAsync(new PolicyGuardRequest("toi muon nhan vien ho tro"), "toi muon nhan vien ho tro");

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_Timeout_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(200, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });

        var classifier = CreateClassifier(handler, new PolicyGuardOptions { ClassifierTimeoutMs = 10, SemanticClassifierMinConfidence = 0.85m });
        var result = await classifier.ClassifyAsync(new PolicyGuardRequest("toi muon nhan vien ho tro"), "toi muon nhan vien ho tro");

        Assert.Null(result);
    }

    private static GeminiPolicyIntentClassifier CreateClassifier(HttpMessageHandler handler, PolicyGuardOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        return new GeminiPolicyIntentClassifier(
            httpClient,
            Options.Create(GeminiOptions),
            Options.Create(options ?? new PolicyGuardOptions { SemanticClassifierMinConfidence = 0.85m }),
            NullLogger<GeminiPolicyIntentClassifier>.Instance);
    }

    private static MockHttpMessageHandler CreateGeminiResponse(string category, decimal confidence, string explanation, string[] matchedSpans)
    {
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
                                    category,
                                    confidence,
                                    explanation,
                                    matchedSpans
                                })
                            }
                        }
                    }
                }
            }
        });

        return MockHttpMessageHandler.CreateWithJsonResponse(responseJson);
    }
}
