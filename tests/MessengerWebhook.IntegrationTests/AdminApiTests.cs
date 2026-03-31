using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests;

public class AdminApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task AdminApi_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/admin/api/draft-orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_And_GetDraftOrders_ReturnsOnlyAssignedPageData()
    {
        var session = await LoginAsync();

        session.Authenticated.Should().BeTrue();
        session.UserEmail.Should().Be(_factory.PrimaryManagerEmail);

        var response = await _client.GetAsync("/admin/api/draft-orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetArrayLength().Should().Be(1);
        payload[0].GetProperty("draftCode").GetString().Should().Be("DR-PRIMARY-001");
        payload[0].GetProperty("facebookPageId").GetString().Should().Be(_factory.PrimaryPageId);
    }

    [Fact]
    public async Task ApproveSubmit_SubmitsToNobita_AndStoresAuditLog()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");

        var response = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var draft = dbContext.DraftOrders.Single(x => x.Id == draftId);
        draft.Status.Should().Be(DraftOrderStatus.SubmittedToNobita);
        draft.NobitaOrderId.Should().NotBeNullOrWhiteSpace();
        _factory.NobitaStub.SubmittedOrders.Should().ContainSingle();
        dbContext.AdminAuditLogs.Should().ContainSingle(x => x.Action == "approve-submit" && x.ResourceId == draftId.ToString());
    }

    [Fact]
    public async Task ResolveSupportCase_ReleasesBotLock()
    {
        var session = await LoginAsync();
        var supportCaseId = await FindSupportCaseIdAsync("psid-case-primary");

        var claimResponse = await PostJsonAsync($"/admin/api/support-cases/{supportCaseId}/claim", session.CsrfToken, new { });
        claimResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolveResponse = await PostJsonAsync(
            $"/admin/api/support-cases/{supportCaseId}/resolve",
            session.CsrfToken,
            new { notes = "Đã gọi xác nhận với khách." });

        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var supportCase = dbContext.HumanSupportCases.Single(x => x.Id == supportCaseId);
        supportCase.Status.Should().Be(SupportCaseStatus.Resolved);
        supportCase.ResolutionNotes.Should().Contain("gọi xác nhận");
        dbContext.BotConversationLocks.Single(x => x.FacebookPSID == "psid-case-primary").IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task SyncNobitaProducts_UpdatesMissingProductMapping()
    {
        var session = await LoginAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            var product = dbContext.Products.Single(x => x.Code == "KL" && x.TenantId == _factory.PrimaryTenantId);
            product.NobitaProductId = null;
            product.NobitaLastSyncedAt = null;
            dbContext.SaveChanges();
        }

        var syncResponse = await PostJsonAsync(
            "/admin/api/nobita/products/sync",
            session.CsrfToken,
            new { search = "KL" });

        syncResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var updatedProduct = verificationDbContext.Products.Single(x => x.Code == "KL" && x.TenantId == _factory.PrimaryTenantId);
        updatedProduct.NobitaProductId.Should().Be(102);
        updatedProduct.NobitaLastSyncedAt.Should().NotBeNull();
    }

    private async Task<(string CsrfToken, bool Authenticated, string? UserEmail)> LoginAsync()
    {
        var preAuth = await GetAuthStateAsync();
        var loginResponse = await PostJsonAsync(
            "/admin/api/auth/login",
            preAuth.CsrfToken,
            new { email = _factory.PrimaryManagerEmail, password = _factory.AdminPassword, rememberMe = true });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await GetAuthStateAsync();
    }

    private async Task<(string CsrfToken, bool Authenticated, string? UserEmail)> GetAuthStateAsync()
    {
        var response = await _client.GetAsync("/admin/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var authenticated = payload.GetProperty("authenticated").GetBoolean();
        var csrfToken = payload.GetProperty("antiForgeryToken").GetString() ?? string.Empty;
        var userEmail = payload.TryGetProperty("user", out var userElement) && userElement.ValueKind != JsonValueKind.Null
            ? userElement.GetProperty("email").GetString()
            : null;

        return (csrfToken, authenticated, userEmail);
    }

    private async Task<Guid> FindDraftIdAsync(string draftCode)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.DraftOrders.Single(x => x.DraftCode == draftCode).Id);
    }

    private async Task<Guid> FindSupportCaseIdAsync(string psid)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.HumanSupportCases.Single(x => x.FacebookPSID == psid).Id);
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string url, string csrfToken, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await _client.SendAsync(request);
    }
}
