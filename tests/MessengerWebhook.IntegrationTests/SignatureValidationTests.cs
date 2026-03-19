using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MessengerWebhook.IntegrationTests;

/// <summary>
/// Integration tests for HMAC-SHA256 signature validation middleware
/// </summary>
public class SignatureValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _appSecret;

    public SignatureValidationTests(WebApplicationFactory<Program> factory)
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
        });

        _client = _factory.CreateClient();
        _appSecret = "test_secret_for_integration_tests";
    }

    private string ComputeSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLower();
    }

    [Fact]
    public async Task PostWebhook_ValidSignature_Returns200()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var signature = ComputeSignature(payload);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("EVENT_RECEIVED");
    }

    [Fact]
    public async Task PostWebhook_InvalidSignature_Returns401()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var invalidSignature = "sha256=0000000000000000000000000000000000000000000000000000000000000000";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", invalidSignature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task PostWebhook_MissingSignatureHeader_Returns401()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        // No X-Hub-Signature-256 header added

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Missing signature");
    }

    [Fact]
    public async Task PostWebhook_MalformedSignatureFormat_Returns401()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var malformedSignature = "abcdef1234567890"; // Missing "sha256=" prefix
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", malformedSignature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task PostWebhook_WrongPrefixFormat_Returns401()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var wrongPrefixSignature = "sha1=abcdef1234567890abcdef1234567890abcdef12";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", wrongPrefixSignature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task PostWebhook_ModifiedPayload_Returns401()
    {
        // Arrange
        var originalPayload = "{\"object\":\"page\",\"entry\":[]}";
        var modifiedPayload = "{\"object\":\"page\",\"entry\":[],\"extra\":\"field\"}";
        var signatureForOriginal = ComputeSignature(originalPayload);
        var content = new StringContent(modifiedPayload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signatureForOriginal);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task PostWebhook_ValidSignature_ComplexPayload_Returns200()
    {
        // Arrange
        var payload = @"{
            ""object"": ""page"",
            ""entry"": [
                {
                    ""id"": ""123456789"",
                    ""time"": 1234567890,
                    ""messaging"": [
                        {
                            ""sender"": { ""id"": ""987654321"" },
                            ""recipient"": { ""id"": ""123456789"" },
                            ""timestamp"": 1234567890,
                            ""message"": {
                                ""mid"": ""mid.1234567890"",
                                ""text"": ""Hello World""
                            }
                        }
                    ]
                }
            ]
        }";
        var signature = ComputeSignature(payload);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostWebhook_EmptyPayload_ValidSignature_Returns401()
    {
        // Arrange
        var payload = "";
        var signature = ComputeSignature(payload);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // Security fix: empty body rejected
    }

    [Fact]
    public async Task GetWebhook_NoSignatureValidation_Returns200()
    {
        // Arrange & Act
        // GET requests should not be validated by signature middleware
        var response = await _client.GetAsync("/webhook?hub.mode=subscribe&hub.verify_token=test_verify_token&hub.challenge=test_challenge");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Be("test_challenge");
    }

    [Fact]
    public async Task PostWebhook_UppercaseHashInSignature_Returns200()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var uppercaseSignature = "sha256=" + Convert.ToHexString(hash).ToUpper();
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", uppercaseSignature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK); // Security fix: hash normalized to lowercase
    }

    [Fact]
    public async Task PostWebhook_SpecialCharactersInPayload_ValidSignature_Returns200()
    {
        // Arrange
        var payload = @"{
            ""object"": ""page"",
            ""entry"": [
                {
                    ""id"": ""123456789"",
                    ""time"": 1234567890,
                    ""messaging"": [
                        {
                            ""sender"": { ""id"": ""USER_ID"" },
                            ""recipient"": { ""id"": ""PAGE_ID"" },
                            ""timestamp"": 1234567890,
                            ""message"": {
                                ""mid"": ""mid.1"",
                                ""text"": ""Hello 世界! 🌍 Special: <>&\""'""
                            }
                        }
                    ]
                }
            ]
        }";
        var signature = ComputeSignature(payload);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostWebhook_LargePayload_ValidSignature_Returns200()
    {
        // Arrange - Create valid webhook payload with large message text
        var largeText = new string('x', 10000);
        var largePayload = @"{
            ""object"": ""page"",
            ""entry"": [
                {
                    ""id"": ""123456789"",
                    ""time"": 1234567890,
                    ""messaging"": [
                        {
                            ""sender"": { ""id"": ""USER_ID"" },
                            ""recipient"": { ""id"": ""PAGE_ID"" },
                            ""timestamp"": 1234567890,
                            ""message"": {
                                ""mid"": ""mid.1"",
                                ""text"": """ + largeText + @"""
                            }
                        }
                    ]
                }
            ]
        }";
        var signature = ComputeSignature(largePayload);
        var content = new StringContent(largePayload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.PostAsync("/webhook", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
