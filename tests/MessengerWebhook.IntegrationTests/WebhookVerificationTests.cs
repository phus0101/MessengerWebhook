using System.Net;

namespace MessengerWebhook.IntegrationTests;

public class WebhookVerificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WebhookVerificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetWebhook_WithValidParameters_Returns200AndChallenge()
    {
        // Arrange
        var mode = "subscribe";
        var verifyToken = "test_verify_token_12345";
        var challenge = "test_challenge_string";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.mode={mode}&hub.verify_token={verifyToken}&hub.challenge={challenge}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(challenge, content);
    }

    [Fact]
    public async Task GetWebhook_WithInvalidMode_Returns403()
    {
        // Arrange
        var mode = "invalid_mode";
        var verifyToken = "test_verify_token_12345";
        var challenge = "test_challenge_string";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.mode={mode}&hub.verify_token={verifyToken}&hub.challenge={challenge}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWebhook_WithInvalidVerifyToken_Returns403()
    {
        // Arrange
        var mode = "subscribe";
        var verifyToken = "wrong_token";
        var challenge = "test_challenge_string";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.mode={mode}&hub.verify_token={verifyToken}&hub.challenge={challenge}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWebhook_WithMissingMode_Returns400()
    {
        // Arrange
        var verifyToken = "test_verify_token_12345";
        var challenge = "test_challenge_string";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.verify_token={verifyToken}&hub.challenge={challenge}");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWebhook_WithMissingVerifyToken_Returns400()
    {
        // Arrange
        var mode = "subscribe";
        var challenge = "test_challenge_string";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.mode={mode}&hub.challenge={challenge}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWebhook_WithMissingChallenge_Returns400()
    {
        // Arrange
        var mode = "subscribe";
        var verifyToken = "test_verify_token_12345";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.mode={mode}&hub.verify_token={verifyToken}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWebhook_WithEmptyParameters_Returns400()
    {
        // Act
        var response = await _client.GetAsync("/webhook");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWebhook_WithEmptyMode_Returns400()
    {
        // Arrange
        var mode = "";
        var verifyToken = "test_verify_token_12345";
        var challenge = "test_challenge_string";

        // Act
        var response = await _client.GetAsync(
            $"/webhook?hub.mode={mode}&hub.verify_token={verifyToken}&hub.challenge={challenge}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
