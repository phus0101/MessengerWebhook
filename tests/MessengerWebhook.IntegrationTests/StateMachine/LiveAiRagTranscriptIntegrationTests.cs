using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using MessengerWebhook.StateMachine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.IntegrationTests.StateMachine;

public class LiveAiRagTranscriptIntegrationTests : IClassFixture<LiveAiRagWebApplicationFactory>
{
    private const string RunLiveTestsEnv = "RUN_LIVE_AI_RAG_TESTS";
    private readonly LiveAiRagWebApplicationFactory _factory;

    public LiveAiRagTranscriptIntegrationTests(LiveAiRagWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MnTranscript_WithLiveAiRag_ShouldKeepCheckoutFlowWhenRememberedContactIsConfirmed()
    {
        if (!ShouldRunLiveTests())
        {
            return;
        }

        using var scope = await CreateStateMachineScopeAsync();
        AssertLiveConfiguration(scope.ServiceProvider);

        var ragService = scope.ServiceProvider.GetRequiredService<IRAGService>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var keywordSearch = scope.ServiceProvider.GetRequiredService<KeywordSearchService>();
        var hybridSearch = scope.ServiceProvider.GetRequiredService<IHybridSearchService>();

        var keywordResults = await keywordSearch.SearchAsync("mặt nạ dưỡng ẩm", topK: 5);
        keywordResults.Should().Contain(
            result => string.Equals(result.ProductId, "product-mn", StringComparison.OrdinalIgnoreCase),
            "live test fixture must expose MN through local keyword search before live RAG preflight runs");

        var hybridResults = await hybridSearch.SearchAsync(
            "mặt nạ dưỡng ẩm",
            topK: 5,
            new Dictionary<string, object> { ["tenant_id"] = _factory.PrimaryTenantId.ToString() });
        hybridResults.Should().Contain(
            result => string.Equals(result.ProductId, "product-mn", StringComparison.OrdinalIgnoreCase),
            "live hybrid search must preserve keyword MN results even when Pinecone has no matching test-tenant fixture");

        var ragContext = await ragService.RetrieveContextAsync("mặt nạ dưỡng ẩm", topK: 5, includeDetailedInfo: true);
        ragContext.Metrics.Source.Should().Be("hybrid", "live RAG must retrieve from the configured hybrid search path, not fallback/error context");
        ragContext.Products.Should().Contain(
            product => string.Equals(product.Code, "MN", StringComparison.OrdinalIgnoreCase),
            "live RAG must already have indexed MN product data before transcript validation runs");

        var psid = $"live-ai-rag-mn-{Guid.NewGuid():N}";
        var pageId = _factory.PrimaryPageId;

        dbContext.CustomerIdentities.Add(new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0888129403",
            ShippingAddress = "4/6/20, ttn1, hcm",
            TenantId = _factory.PrimaryTenantId
        });
        await dbContext.SaveChangesAsync();

        await stateMachine.ProcessMessageAsync(psid, "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", pageId);
        await stateMachine.ProcessMessageAsync(psid, "1", pageId);
        await stateMachine.ProcessMessageAsync(psid, "tư vấn thêm về công dụng và cách dùng", pageId);
        await stateMachine.ProcessMessageAsync(psid, "nói kỹ hơn", pageId);
        var checkoutPrompt = await stateMachine.ProcessMessageAsync(psid, "lên đơn cho tôi", pageId);

        checkoutPrompt.Should().Contain("0888129403");
        checkoutPrompt.Should().ContainEquivalentOf("4/6/20");
        checkoutPrompt.Should().NotContainEquivalentOf("không tìm thấy sản phẩm phù hợp");
        checkoutPrompt.Should().NotContainEquivalentOf("chưa tìm thấy dữ liệu sản phẩm phù hợp");

        var finalResponse = await stateMachine.ProcessMessageAsync(psid, "vẫn dùng thông tin cũ", pageId);

        finalResponse.Should().ContainEquivalentOf("tóm tắt đơn");
        finalResponse.Should().ContainEquivalentOf("Mat Na");
        finalResponse.Should().Contain("0888129403");
        finalResponse.Should().ContainEquivalentOf("4/6/20");
        finalResponse.Should().NotContainEquivalentOf("không tìm thấy sản phẩm phù hợp");
        finalResponse.Should().NotContainEquivalentOf("chưa tìm thấy dữ liệu sản phẩm phù hợp");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();
    }

    private async Task<IServiceScope> CreateStateMachineScopeAsync()
    {
        var scope = await _factory.CreateIsolatedScopeAsync();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Initialize(_factory.PrimaryTenantId, _factory.PrimaryPageId, _factory.PrimaryManagerEmail);
        return scope;
    }

    private static bool ShouldRunLiveTests()
    {
        return string.Equals(Environment.GetEnvironmentVariable(RunLiveTestsEnv), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertLiveConfiguration(IServiceProvider serviceProvider)
    {
        var geminiOptions = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
        var pineconeOptions = serviceProvider.GetRequiredService<IOptions<PineconeOptions>>().Value;
        var vertexOptions = serviceProvider.GetRequiredService<IOptions<VertexAIOptions>>().Value;

        geminiOptions.ApiKey.Should().NotBeNullOrWhiteSpace("GEMINI_API_KEY is required when RUN_LIVE_AI_RAG_TESTS=true");
        pineconeOptions.ApiKey.Should().NotBeNullOrWhiteSpace("PINECONE_API_KEY is required when RUN_LIVE_AI_RAG_TESTS=true");
        vertexOptions.ProjectId.Should().NotBeNullOrWhiteSpace("VERTEX_AI_PROJECT_ID is required when RUN_LIVE_AI_RAG_TESTS=true");
        vertexOptions.ServiceAccountKeyPath.Should().NotBeNullOrWhiteSpace("VERTEX_AI_SERVICE_ACCOUNT_KEY_PATH is required when RUN_LIVE_AI_RAG_TESTS=true");
        File.Exists(vertexOptions.ServiceAccountKeyPath).Should().BeTrue("VERTEX_AI_SERVICE_ACCOUNT_KEY_PATH must point to a readable service account key file");
    }
}
