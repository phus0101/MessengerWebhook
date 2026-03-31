using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;

namespace MessengerWebhook.IntegrationTests;

public class SignatureValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignatureValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostWebhook_ValidSignature_Returns200()
    {
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var response = await PostWithSignatureAsync(payload, ComputeSignature(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EVENT_RECEIVED");
    }

    [Fact]
    public async Task PostWebhook_InvalidSignature_Returns401()
    {
        var response = await PostWithSignatureAsync(
            "{\"object\":\"page\",\"entry\":[]}",
            "sha256=0000000000000000000000000000000000000000000000000000000000000000");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task PostWebhook_MissingSignatureHeader_Returns401()
    {
        var content = new StringContent("{\"object\":\"page\",\"entry\":[]}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Missing signature");
    }

    [Fact]
    public async Task PostWebhook_ModifiedPayload_Returns401()
    {
        var signatureForOriginal = ComputeSignature("{\"object\":\"page\",\"entry\":[]}");
        var response = await PostWithSignatureAsync(
            "{\"object\":\"page\",\"entry\":[],\"extra\":\"field\"}",
            signatureForOriginal);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task PostWebhook_EmptyPayload_ValidSignature_Returns401()
    {
        var response = await PostWithSignatureAsync(string.Empty, ComputeSignature(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWebhook_NoSignatureValidation_Returns200()
    {
        var response = await _client.GetAsync($"/webhook?hub.mode=subscribe&hub.verify_token={_factory.VerifyToken}&hub.challenge=test_challenge");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("test_challenge");
    }

    [Fact]
    public async Task PostWebhook_UppercaseHashInSignature_Returns200()
    {
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var response = await PostWithSignatureAsync(payload, "sha256=" + Convert.ToHexString(hash).ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> PostWithSignatureAsync(string payload, string signature)
    {
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);
        return await _client.PostAsync("/webhook", content);
    }

    private string ComputeSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
