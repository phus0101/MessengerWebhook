using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.StateMachine;

public class ReturningCustomerConfirmationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReturningCustomerConfirmationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReturningCustomer_ConfirmsWithDungRoi_ShouldCreateDraftOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = "test-page-123";

        // Create existing customer with previous order
        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0912345678",
            ShippingAddress = "123 Test Street, District 1, HCMC",
            TenantId = Guid.NewGuid()
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        // Act - Step 1: Customer asks about product
        var response1 = await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);

        // Assert - Bot should offer product and ask for confirmation
        Assert.Contains("kem chong nang", response1.ToLower());

        // Act - Step 2: Customer confirms with "đúng rồi"
        var response2 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);

        // Assert - Bot should create draft order
        Assert.Contains("len don", response2.ToLower());

        // Verify draft order was created in database
        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.NotNull(draftOrder);
        Assert.Equal(customer.PhoneNumber, draftOrder.CustomerPhone);
        Assert.Equal(customer.ShippingAddress, draftOrder.ShippingAddress);
    }

    [Fact]
    public async Task ReturningCustomer_ConfirmsWithOkEm_ShouldCreateDraftOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = "test-page-123";

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0987654321",
            ShippingAddress = "456 Another Street, District 3, HCMC",
            TenantId = Guid.NewGuid()
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        // Act - Step 1: Customer asks about product
        await stateMachine.ProcessMessageAsync(psid, "cho em kem lua", pageId);

        // Act - Step 2: Customer confirms with "ok em"
        var response2 = await stateMachine.ProcessMessageAsync(psid, "ok em", pageId);

        // Assert
        Assert.Contains("len don", response2.ToLower());

        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.NotNull(draftOrder);
    }

    [Fact]
    public async Task ReturningCustomer_DoesNotConfirm_ShouldNotCreateDraftOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = "test-page-123";

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0912345678",
            ShippingAddress = "123 Test Street, District 1, HCMC",
            TenantId = Guid.NewGuid()
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        // Act - Step 1: Customer asks about product
        await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);

        // Act - Step 2: Customer asks unrelated question (not confirming)
        var response2 = await stateMachine.ProcessMessageAsync(psid, "kem nay co tot khong?", pageId);

        // Assert - Should not create draft order yet
        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrder);
    }
}
