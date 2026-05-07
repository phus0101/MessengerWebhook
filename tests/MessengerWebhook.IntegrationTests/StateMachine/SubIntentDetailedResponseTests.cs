using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Models;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.StateMachine;

/// <summary>
/// Integration test verifying SubIntent classification fixes the "nói kỹ hơn" (tell me more) issue.
/// Based on real conversation transcript where bot repeated answers instead of providing details.
/// </summary>
public class SubIntentDetailedResponseTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SubIntentDetailedResponseTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NoiKyHon_ShouldDetectProductQuestionSubIntent()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"subintent-noi-ky-hon-{Guid.NewGuid()}";

        // Step 1: Customer asks about product
        await stateMachine.ProcessMessageAsync(psid, "cho em xem sữa rửa mặt", _factory.PrimaryPageId);

        // Step 2: Customer asks "nói kỹ hơn" (tell me more)
        await stateMachine.ProcessMessageAsync(psid, "nói kỹ hơn", _factory.PrimaryPageId);

        // Verify SubIntent was detected
        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        var subIntent = context.GetData<SubIntentResult?>("subIntent");

        subIntent.Should().NotBeNull("SubIntent should be detected for 'nói kỹ hơn'");
        subIntent!.Category.Should().Be(SubIntentCategory.ProductQuestion, "SubIntent should be ProductQuestion");
        subIntent.Confidence.Should().BeGreaterThan(0.5m, "SubIntent confidence should be high");
        subIntent.Source.Should().Be("test-mock", "SubIntent should come from test mock");
    }

    [Fact]
    public async Task ChiTiet_ShouldDetectProductQuestionSubIntent()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"subintent-chi-tiet-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(psid, "cho em biết chi tiết hơn", _factory.PrimaryPageId);

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        var subIntent = context.GetData<SubIntentResult?>("subIntent");

        subIntent.Should().NotBeNull();
        subIntent!.Category.Should().Be(SubIntentCategory.ProductQuestion);
        subIntent.Confidence.Should().BeGreaterThan(0.5m);
    }

    [Fact]
    public async Task GiaBaoNhieu_ShouldDetectPriceQuestionSubIntent()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"subintent-price-{Guid.NewGuid()}";

        // Step 1: Establish product context
        var reply1 = await stateMachine.ProcessMessageAsync(psid, "cho em xem mặt nạ ngủ", _factory.PrimaryPageId);
        Console.WriteLine($"[DEBUG] Reply 1: {reply1}");

        // Step 2: Ask about price
        var reply2 = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu", _factory.PrimaryPageId);
        Console.WriteLine($"[DEBUG] Reply 2: {reply2}");

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        var subIntent = context.GetData<SubIntentResult?>("subIntent");

        Console.WriteLine($"[DEBUG] SubIntent: {subIntent?.Category}, Confidence: {subIntent?.Confidence}, Source: {subIntent?.Source}");

        subIntent.Should().NotBeNull();
        subIntent!.Category.Should().Be(SubIntentCategory.PriceQuestion);
        subIntent.Confidence.Should().BeGreaterThan(0.5m);
        subIntent.Source.Should().Be("test-mock");
    }

    [Fact]
    public async Task SubIntentContext_ShouldBeStoredInStateContext()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"subintent-storage-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "sữa rửa mặt", _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(psid, "nói kỹ hơn", _factory.PrimaryPageId);

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        var subIntent = context.GetData<SubIntentResult?>("subIntent");

        // Verify all SubIntent fields are properly stored
        subIntent.Should().NotBeNull();
        subIntent!.Category.Should().NotBe(default(SubIntentCategory));
        subIntent.Confidence.Should().BeGreaterThan(0m);
        subIntent.Source.Should().NotBeNullOrEmpty();
    }

    private async Task<IServiceScope> CreateStateMachineScopeAsync()
    {
        var scope = await _factory.CreateIsolatedScopeAsync();
        InitializeTenantContext(scope);
        return scope;
    }

    private ITenantContext InitializeTenantContext(IServiceScope scope)
    {
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Initialize(_factory.PrimaryTenantId, _factory.PrimaryPageId, _factory.PrimaryManagerEmail);
        return tenantContext;
    }
}
