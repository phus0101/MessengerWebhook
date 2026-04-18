using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests;

public sealed record AdminSession(
    string CsrfToken,
    bool Authenticated,
    string? UserEmail,
    bool CanAccessAllPagesInTenant,
    string? VisibilityMode);

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
    public async Task Login_And_GetDraftOrders_ReturnsOnlyAssignedPageData_InStrictMode()
    {
        var session = await LoginAsync();

        session.Authenticated.Should().BeTrue();
        session.UserEmail.Should().Be(_factory.PrimaryManagerEmail);
        session.CanAccessAllPagesInTenant.Should().BeFalse();
        session.VisibilityMode.Should().Be("page-scoped");

        var response = await _client.GetAsync("/admin/api/draft-orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetArrayLength().Should().Be(1);
        payload[0].GetProperty("draftCode").GetString().Should().Be("DR-PRIMARY-001");
        payload[0].GetProperty("facebookPageId").GetString().Should().Be(_factory.PrimaryPageId);
        payload[0].GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        payload[0].GetProperty("status").GetString().Should().Be("PendingReview");
        payload[0].GetProperty("riskLevel").ValueKind.Should().Be(JsonValueKind.String);
        payload[0].GetProperty("riskLevel").GetString().Should().Be("Low");
        payload[0].GetProperty("priceConfirmed").GetBoolean().Should().BeTrue();
        payload[0].GetProperty("shippingConfirmed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AdminApi_DetailEndpoints_SerializeEnumFields_AsStrings()
    {
        await LoginAsync();

        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");
        var draftResponse = await _client.GetAsync($"/admin/api/draft-orders/{draftId}");
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draftPayload = JsonDocument.Parse(await draftResponse.Content.ReadAsStringAsync()).RootElement;
        draftPayload.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        draftPayload.GetProperty("status").GetString().Should().Be("PendingReview");
        draftPayload.GetProperty("riskLevel").ValueKind.Should().Be(JsonValueKind.String);
        draftPayload.GetProperty("riskLevel").GetString().Should().Be("Low");
        draftPayload.GetProperty("priceConfirmed").GetBoolean().Should().BeTrue();
        draftPayload.GetProperty("shippingConfirmed").GetBoolean().Should().BeTrue();

        var supportCaseId = await FindSupportCaseIdAsync("psid-case-primary");
        var supportResponse = await _client.GetAsync($"/admin/api/support-cases/{supportCaseId}");
        supportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var supportPayload = JsonDocument.Parse(await supportResponse.Content.ReadAsStringAsync()).RootElement;
        supportPayload.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        supportPayload.GetProperty("status").GetString().Should().Be("Open");
        supportPayload.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.String);
        supportPayload.GetProperty("reason").GetString().Should().Be("PolicyException");
    }

    [Fact]
    public async Task SearchCustomers_ReturnsOnlyCustomersWithinAllowedScope()
    {
        await LoginAsync();

        var response = await _client.GetAsync("/admin/api/customers?query=Khach%20Gan%20Lai");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetArrayLength().Should().Be(1);
        payload[0].GetProperty("fullName").GetString().Should().Be("Khach Gan Lai");
        payload[0].GetProperty("phoneNumber").GetString().Should().Be("0911111111");
    }

    [Fact]
    public async Task UpdateDraftOrder_UsesSelectedCustomerOnlyAsPrefillMetadata()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");
        var customerId = await FindCustomerIdAsync("psid-primary-existing");
        var originalCustomerId = await FindCustomerIdAsync("psid-primary");

        var response = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/update",
            session.CsrfToken,
            new
            {
                customerIdentityId = customerId,
                customerName = "Thong tin da sua tren draft",
                customerPhone = "0999999999",
                shippingAddress = "Dia chi tam",
                items = new object[]
                {
                    new { productCode = "KCN", quantity = 1, giftCode = "GIFT_KCN" }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetProperty("succeeded").GetBoolean().Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var draft = dbContext.DraftOrders.Single(x => x.Id == draftId);
        draft.CustomerIdentityId.Should().Be(originalCustomerId);
        draft.CustomerName.Should().Be("Thong tin da sua tren draft");
        draft.CustomerPhone.Should().Be("0999999999");
        draft.ShippingAddress.Should().Be("Dia chi tam");
        draft.FacebookPageId.Should().Be(_factory.PrimaryPageId);
    }

    [Fact]
    public async Task UpdateDraftOrder_UpdatesCustomerItemsAndTotals()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");

        var response = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/update",
            session.CsrfToken,
            new
            {
                customerName = "Khach da sua",
                customerPhone = "0988888888",
                shippingAddress = "9 Le Loi, Quan 1",
                items = new object[]
                {
                    new { productCode = "KCN", quantity = 1, giftCode = "GIFT_KCN" },
                    new { productCode = "KL", quantity = 2, giftCode = "GIFT_KL" }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetProperty("succeeded").GetBoolean().Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var draft = dbContext.DraftOrders
            .Include(x => x.Items)
            .Single(x => x.Id == draftId);

        draft.CustomerName.Should().Be("Khach da sua");
        draft.CustomerPhone.Should().Be("0988888888");
        draft.ShippingAddress.Should().Be("9 Le Loi, Quan 1");
        draft.Status.Should().Be(DraftOrderStatus.PendingReview);
        draft.MerchandiseTotal.Should().Be(1140000m);
        draft.ShippingFee.Should().Be(30000m);
        draft.GrandTotal.Should().Be(1170000m);
        draft.PriceConfirmed.Should().BeTrue();
        draft.ShippingConfirmed.Should().BeTrue();
        draft.Items.Should().HaveCount(2);
        draft.Items.Should().Contain(x => x.ProductCode == "KL" && x.Quantity == 2 && x.GiftCode == "GIFT_KL");
        dbContext.AdminAuditLogs.Should().ContainSingle(x => x.Action == "update-draft" && x.ResourceId == draftId.ToString());
    }

    [Fact]
    public async Task UpdateDraftOrder_DoesNotAllowSubmittedDraft()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            var draft = dbContext.DraftOrders.Single(x => x.Id == draftId);
            draft.Status = DraftOrderStatus.SubmittedToNobita;
            draft.NobitaOrderId = "NB-EXISTING";
            dbContext.SaveChanges();
        }

        var response = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/update",
            session.CsrfToken,
            new
            {
                customerName = "Khach bi khoa",
                customerPhone = "0900000999",
                shippingAddress = "10 Nguyen Hue",
                items = new object[]
                {
                    new { productCode = "KCN", quantity = 1, giftCode = "GIFT_KCN" }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetProperty("succeeded").GetBoolean().Should().BeFalse();
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
        draft.CustomerMetricsAppliedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveSubmit_UpdatesCustomerMetricsOnlyOnce()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");
        var customerId = await FindCustomerIdAsync("psid-primary");

        using (var beforeScope = _factory.Services.CreateScope())
        {
            var dbContext = beforeScope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            var customer = dbContext.CustomerIdentities.Single(x => x.Id == customerId);
            customer.TotalOrders.Should().Be(0);
            customer.LifetimeValue.Should().Be(0);
        }

        var response = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var afterScope = _factory.Services.CreateScope();
        var afterDbContext = afterScope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var updatedCustomer = afterDbContext.CustomerIdentities.Single(x => x.Id == customerId);
        var updatedDraft = afterDbContext.DraftOrders.Single(x => x.Id == draftId);
        updatedCustomer.TotalOrders.Should().Be(1);
        updatedCustomer.LifetimeValue.Should().Be(320000m);
        updatedDraft.CustomerMetricsAppliedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveSubmit_DoesNotCreateDuplicateOrderOrMetrics_OnSecondAttempt()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");
        var customerId = await FindCustomerIdAsync("psid-primary");

        var firstResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync()).RootElement;
        secondPayload.GetProperty("succeeded").GetBoolean().Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var customer = dbContext.CustomerIdentities.Single(x => x.Id == customerId);
        customer.TotalOrders.Should().Be(1);
        customer.LifetimeValue.Should().Be(320000m);
        _factory.NobitaStub.SubmittedOrders.Should().ContainSingle();
        dbContext.AdminAuditLogs.Count(x => x.Action == "approve-submit" && x.ResourceId == draftId.ToString()).Should().Be(1);
    }

    [Fact]
    public async Task RetrySubmit_AfterFailure_Succeeds_AndAppliesMetricsOnce()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");
        var customerId = await FindCustomerIdAsync("psid-primary");
        _factory.NobitaStub.FailNextOrderSubmission = true;

        var failedResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });
        failedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var failedPayload = JsonDocument.Parse(await failedResponse.Content.ReadAsStringAsync()).RootElement;
        failedPayload.GetProperty("succeeded").GetBoolean().Should().BeFalse();

        var retryResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/retry-submit",
            session.CsrfToken,
            new { });
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryPayload = JsonDocument.Parse(await retryResponse.Content.ReadAsStringAsync()).RootElement;
        retryPayload.GetProperty("succeeded").GetBoolean().Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var draft = dbContext.DraftOrders.Single(x => x.Id == draftId);
        var customer = dbContext.CustomerIdentities.Single(x => x.Id == customerId);
        draft.Status.Should().Be(DraftOrderStatus.SubmittedToNobita);
        draft.SubmissionClaimedAt.Should().BeNull();
        draft.CustomerMetricsAppliedAt.Should().NotBeNull();
        customer.TotalOrders.Should().Be(1);
        customer.LifetimeValue.Should().Be(320000m);
        _factory.NobitaStub.SubmittedOrders.Should().ContainSingle();
        dbContext.AdminAuditLogs.Count(x => x.Action == "approve-submit" && x.ResourceId == draftId.ToString()).Should().Be(1);
        dbContext.AdminAuditLogs.Count(x => x.Action == "submit-failed" && x.ResourceId == draftId.ToString()).Should().Be(1);
    }

    [Fact]
    public async Task ApproveSubmit_BlocksWhenShippingOrPriceNotConfirmed()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            var draft = dbContext.DraftOrders.Single(x => x.Id == draftId);
            draft.ShippingConfirmed = false;
            dbContext.SaveChanges();
        }

        var response = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetProperty("succeeded").GetBoolean().Should().BeFalse();
        payload.GetProperty("message").GetString().Should().Contain("chốt đủ giá và phí ship");
        _factory.NobitaStub.SubmittedOrders.Should().BeEmpty();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        verificationDbContext.DraftOrders.Single(x => x.Id == draftId).SubmissionClaimedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApproveSubmit_UsesLatestDraftData_AfterUpdate()
    {
        var session = await LoginAsync();
        var draftId = await FindDraftIdAsync("DR-PRIMARY-001");

        var updateResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/update",
            session.CsrfToken,
            new
            {
                customerName = "Khach submit moi",
                customerPhone = "0977777777",
                shippingAddress = "11 Tran Hung Dao",
                items = new object[]
                {
                    new { productCode = "KCN", quantity = 1, giftCode = "GIFT_KCN" },
                    new { productCode = "KL", quantity = 1, giftCode = "GIFT_KL" }
                }
            });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.NobitaStub.SubmittedOrders.Should().ContainSingle();
        var submittedOrder = _factory.NobitaStub.SubmittedOrders.Single();
        submittedOrder.CustomerName.Should().Be("Khach submit moi");
        submittedOrder.CustomerPhoneNumber.Should().Be("0977777777");
        submittedOrder.ShippingAddress.Should().Be("11 Tran Hung Dao");
        submittedOrder.Details.Should().HaveCount(2);
        submittedOrder.Details.Should().Contain(x => x.ProductId == 101 && x.Quantity == 1);
        submittedOrder.Details.Should().Contain(x => x.ProductId == 102 && x.Quantity == 1);
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
            new { notes = "Da goi xac nhan voi khach." });

        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var supportCase = dbContext.HumanSupportCases.Single(x => x.Id == supportCaseId);
        supportCase.Status.Should().Be(SupportCaseStatus.Resolved);
        supportCase.ResolutionNotes.Should().Contain("xac nhan");
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

    protected async Task<AdminSession> LoginAsync()
    {
        var preAuth = await GetAuthStateAsync();
        var loginResponse = await PostJsonAsync(
            "/admin/api/auth/login",
            preAuth.CsrfToken,
            new { email = _factory.PrimaryManagerEmail, password = _factory.AdminPassword, rememberMe = true });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await GetAuthStateAsync();
    }

    protected async Task<AdminSession> GetAuthStateAsync()
    {
        var response = await _client.GetAsync("/admin/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var authenticated = payload.GetProperty("authenticated").GetBoolean();
        var csrfToken = payload.GetProperty("antiForgeryToken").GetString() ?? string.Empty;
        var userEmail = payload.TryGetProperty("user", out var userElement) && userElement.ValueKind != JsonValueKind.Null
            ? userElement.GetProperty("email").GetString()
            : null;
        var canAccessAllPagesInTenant = payload.TryGetProperty("user", out userElement) &&
                                        userElement.ValueKind != JsonValueKind.Null &&
                                        userElement.TryGetProperty("canAccessAllPagesInTenant", out var visibilityElement) &&
                                        visibilityElement.GetBoolean();
        var visibilityMode = payload.TryGetProperty("user", out userElement) &&
                             userElement.ValueKind != JsonValueKind.Null &&
                             userElement.TryGetProperty("visibilityMode", out var visibilityModeElement)
            ? visibilityModeElement.GetString()
            : null;

        return new AdminSession(csrfToken, authenticated, userEmail, canAccessAllPagesInTenant, visibilityMode);
    }

    protected async Task<Guid> FindDraftIdAsync(string draftCode)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.DraftOrders.Single(x => x.DraftCode == draftCode).Id);
    }

    protected async Task<Guid> FindSupportCaseIdAsync(string psid)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.HumanSupportCases.Single(x => x.FacebookPSID == psid).Id);
    }

    protected async Task<Guid> FindCustomerIdAsync(string psid)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.CustomerIdentities.Single(x => x.FacebookPSID == psid).Id);
    }

    protected async Task<HttpResponseMessage> PostJsonAsync(string url, string csrfToken, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await _client.SendAsync(request);
    }

}

public sealed class DevelopmentAdminApiTests : IClassFixture<DevelopmentAdminWebApplicationFactory>
{
    private readonly DevelopmentAdminWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DevelopmentAdminApiTests(DevelopmentAdminWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Login_InDevelopment_SeesAllPagesWithinTenant_ButNotOtherTenants()
    {
        var session = await LoginAsync();

        session.CanAccessAllPagesInTenant.Should().BeTrue();
        session.VisibilityMode.Should().Be("tenant-wide");

        var response = await _client.GetAsync("/admin/api/draft-orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var draftCodes = payload.EnumerateArray().Select(x => x.GetProperty("draftCode").GetString()).ToArray();
        draftCodes.Should().Contain("DR-PRIMARY-001");
        draftCodes.Should().Contain("DR-PRIMARY-ALT-001");
        draftCodes.Should().NotContain("DR-SECONDARY-001");
    }

    [Fact]
    public async Task Login_InDevelopment_SeesSupportCasesAcrossPagesInSameTenant()
    {
        var session = await LoginAsync();

        var response = await _client.GetAsync("/admin/api/support-cases");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var psids = payload.EnumerateArray().Select(x => x.GetProperty("facebookPSID").GetString()).ToArray();
        psids.Should().Contain("psid-case-primary");
        psids.Should().Contain("psid-case-primary-alt");
        psids.Should().NotContain("psid-case-secondary");

        var draftId = await FindDraftIdAsync("DR-PRIMARY-ALT-001");
        var submitResponse = await PostJsonAsync(
            $"/admin/api/draft-orders/{draftId}/approve-submit",
            session.CsrfToken,
            new { });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<AdminSession> LoginAsync()
    {
        var preAuth = await GetAuthStateAsync();
        var loginResponse = await PostJsonAsync(
            "/admin/api/auth/login",
            preAuth.CsrfToken,
            new { email = _factory.PrimaryManagerEmail, password = _factory.AdminPassword, rememberMe = true });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await GetAuthStateAsync();
    }

    private async Task<AdminSession> GetAuthStateAsync()
    {
        var response = await _client.GetAsync("/admin/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var authenticated = payload.GetProperty("authenticated").GetBoolean();
        var csrfToken = payload.GetProperty("antiForgeryToken").GetString() ?? string.Empty;
        var userElement = payload.GetProperty("user");

        if (userElement.ValueKind == JsonValueKind.Null)
        {
            return new AdminSession(csrfToken, authenticated, null, false, null);
        }

        return new AdminSession(
            csrfToken,
            authenticated,
            userElement.GetProperty("email").GetString(),
            userElement.GetProperty("canAccessAllPagesInTenant").GetBoolean(),
            userElement.GetProperty("visibilityMode").GetString());
    }

    private async Task<Guid> FindDraftIdAsync(string draftCode)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.DraftOrders.Single(x => x.DraftCode == draftCode).Id);
    }

    private async Task<Guid> FindCustomerIdAsync(string psid)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        return await Task.FromResult(dbContext.CustomerIdentities.Single(x => x.FacebookPSID == psid).Id);
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
