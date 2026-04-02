using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.RAG;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services;

/// <summary>
/// RAG Integration Tests
/// NOTE: These tests are currently skipped due to EF Core InMemory limitation.
/// InMemory provider does not support pgvector extension (Vector type).
///
/// To run these tests, either:
/// 1. Use a real PostgreSQL database with pgvector extension
/// 2. Mock the entire DbContext in CustomWebApplicationFactory
///
/// Unit tests in RAGServiceTests.cs provide adequate coverage for RAG logic.
/// </summary>
public class RAGIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RAGIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RAGService_RetrieveContext_ReturnsValidStructure()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var ragService = scope.ServiceProvider.GetRequiredService<IRAGService>();

        // Act
        var result = await ragService.RetrieveContextAsync("kem chống nắng", topK: 3);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FormattedContext);
        Assert.NotNull(result.ProductIds);
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.TotalLatency.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task RAGService_EmptyQuery_ReturnsEmptyContext()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var ragService = scope.ServiceProvider.GetRequiredService<IRAGService>();

        // Act
        var result = await ragService.RetrieveContextAsync("", topK: 5);

        // Assert
        Assert.NotNull(result.FormattedContext);
        Assert.Empty(result.ProductIds);
        Assert.Equal(0, result.Metrics.ProductsRetrieved);
    }

    [Fact]
    public async Task RAGService_Metrics_TrackPerformance()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var ragService = scope.ServiceProvider.GetRequiredService<IRAGService>();

        // Act
        var result = await ragService.RetrieveContextAsync("sản phẩm", topK: 3);

        // Assert
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.RetrievalLatency.TotalMilliseconds >= 0);
        Assert.True(result.Metrics.TotalLatency.TotalMilliseconds >= result.Metrics.RetrievalLatency.TotalMilliseconds);
        Assert.InRange(result.Metrics.ProductsRetrieved, 0, 3);
    }

    [Fact]
    public async Task GeminiService_WithRAGContext_AcceptsParameter()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();
        var ragContext = "Sản phẩm liên quan:\n1. Test Product - 100,000đ";

        // Act
        var response = await geminiService.SendMessageAsync(
            "test-user",
            "Tôi cần kem chống nắng",
            new List<MessengerWebhook.Services.AI.Models.ConversationMessage>(),
            ragContext: ragContext);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);
    }

    [Fact]
    public void RAGOptions_LoadsFromConfiguration()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var ragOptions = scope.ServiceProvider.GetRequiredService<IOptions<RAGOptions>>();

        // Assert
        Assert.NotNull(ragOptions.Value);
        Assert.False(ragOptions.Value.Enabled); // Default is disabled
        Assert.Equal(5, ragOptions.Value.TopK);
        Assert.Equal("full-context", ragOptions.Value.FallbackStrategy);
        Assert.Equal(5000, ragOptions.Value.TimeoutMs);
    }

    [Fact]
    public async Task ContextAssembler_FormatsVietnameseContext()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var contextAssembler = scope.ServiceProvider.GetRequiredService<ContextAssembler>();
        var productIds = new List<string> { "test-id-1", "test-id-2" };

        // Act
        var context = await contextAssembler.AssembleContextAsync(productIds);

        // Assert
        Assert.NotNull(context);
        Assert.Contains("Sản phẩm liên quan:", context);
    }
}
