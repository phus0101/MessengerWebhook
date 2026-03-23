using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Strategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services;

/// <summary>
/// Integration tests for Gemini API - verifies real API calls work with current models
/// </summary>
public class GeminiApiIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IModelSelectionStrategy _modelStrategy;
    private readonly ILogger<GeminiService> _geminiLogger;
    private readonly ILogger<GeminiEmbeddingService> _embeddingLogger;

    public GeminiApiIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _options = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>()
            ?? throw new InvalidOperationException("Gemini configuration not found");

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);

        _modelStrategy = new HybridModelSelectionStrategy();
        _geminiLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<GeminiService>();
        _embeddingLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<GeminiEmbeddingService>();
    }

    [Fact(Skip = "Integration test - requires valid API key")]
    public async Task GeminiService_SendMessage_WithFlashLiteModel_ShouldSucceed()
    {
        // Arrange
        var service = new GeminiService(
            _httpClient,
            Options.Create(_options),
            _modelStrategy,
            _geminiLogger);

        var testMessage = "Xin chào! Bạn có thể giới thiệu ngắn gọn về bản thân không?";

        // Act
        var response = await service.SendMessageAsync(
            "test-user-123",
            testMessage,
            new List<MessengerWebhook.Services.AI.Models.ConversationMessage>());

        // Assert
        response.Should().NotBeNullOrWhiteSpace();
        response.Length.Should().BeGreaterThan(10);
    }

    [Fact(Skip = "Integration test - requires valid API key")]
    public async Task GeminiEmbeddingService_GenerateAsync_WithEmbedding2Model_ShouldSucceed()
    {
        // Arrange
        var service = new GeminiEmbeddingService(
            _httpClient,
            Options.Create(_options),
            _embeddingLogger);

        var testText = "Kem dưỡng ẩm cho da dầu";

        // Act
        var embedding = await service.GenerateAsync(testText);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().BeGreaterThan(0);
        embedding.Should().AllSatisfy(v => v.Should().NotBe(0f)); // At least some values should be non-zero
    }

    [Fact(Skip = "Integration test - requires valid API key")]
    public async Task GeminiEmbeddingService_GenerateBatchAsync_WithEmbedding2Model_ShouldSucceed()
    {
        // Arrange
        var service = new GeminiEmbeddingService(
            _httpClient,
            Options.Create(_options),
            _embeddingLogger);

        var testTexts = new List<string>
        {
            "Kem dưỡng ẩm cho da dầu",
            "Serum vitamin C",
            "Sữa rửa mặt cho da nhạy cảm"
        };

        // Act
        var embeddings = await service.GenerateBatchAsync(testTexts);

        // Assert
        embeddings.Should().HaveCount(3);
        embeddings.Should().AllSatisfy(emb =>
        {
            emb.Should().NotBeNull();
            emb.Length.Should().BeGreaterThan(0);
        });
    }

    [Fact(Skip = "Integration test - requires valid API key")]
    public async Task GeminiService_SendMessage_WithConversationHistory_ShouldSucceed()
    {
        // Arrange
        var service = new GeminiService(
            _httpClient,
            Options.Create(_options),
            _modelStrategy,
            _geminiLogger);

        var history = new List<MessengerWebhook.Services.AI.Models.ConversationMessage>
        {
            new() { Role = "user", Content = "Tôi có da dầu", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "model", Content = "Tôi hiểu bạn có da dầu. Tôi có thể giúp gì cho bạn?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        };

        var testMessage = "Bạn có thể gợi ý sản phẩm phù hợp không?";

        // Act
        var response = await service.SendMessageAsync("test-user-123", testMessage, history);

        // Assert
        response.Should().NotBeNullOrWhiteSpace();
        response.Should().Contain("sản phẩm", "Response should mention products");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
