using MessengerWebhook.Models;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MessengerWebhook.IntegrationTests;

public class TenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TenantIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Products_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        // Create products for two different tenants (no tenant context)
        tenantContext.Clear();
        var product1 = new Product
        {
            TenantId = tenant1Id,
            Code = $"T1_PROD_{Guid.NewGuid():N}",
            Name = "Tenant 1 Product",
            BasePrice = 100,
            IsActive = true
        };

        var product2 = new Product
        {
            TenantId = tenant2Id,
            Code = $"T2_PROD_{Guid.NewGuid():N}",
            Name = "Tenant 2 Product",
            BasePrice = 200,
            IsActive = true
        };

        dbContext.Products.AddRange(product1, product2);
        await dbContext.SaveChangesAsync();

        // Clear change tracker to force fresh queries
        dbContext.ChangeTracker.Clear();

        // Query as Tenant 1
        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Products = await dbContext.Products
            .Where(p => p.Code == product1.Code || p.Code == product2.Code)
            .ToListAsync();

        Assert.Single(tenant1Products);
        Assert.Equal(product1.Code, tenant1Products[0].Code);

        // Clear and query as Tenant 2
        dbContext.ChangeTracker.Clear();
        tenantContext.Initialize(tenant2Id, "page2", null);
        var tenant2Products = await dbContext.Products
            .Where(p => p.Code == product1.Code || p.Code == product2.Code)
            .ToListAsync();

        Assert.Single(tenant2Products);
        Assert.Equal(product2.Code, tenant2Products[0].Code);
    }

    [Fact]
    public async Task CustomerIdentities_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.Clear();
        var customer1 = new CustomerIdentity
        {
            TenantId = tenant1Id,
            FacebookPSID = $"psid_t1_{Guid.NewGuid():N}",
            FacebookPageId = "page1"
        };

        var customer2 = new CustomerIdentity
        {
            TenantId = tenant2Id,
            FacebookPSID = $"psid_t2_{Guid.NewGuid():N}",
            FacebookPageId = "page2"
        };

        dbContext.CustomerIdentities.AddRange(customer1, customer2);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Customers = await dbContext.CustomerIdentities
            .Where(c => c.FacebookPSID == customer1.FacebookPSID || c.FacebookPSID == customer2.FacebookPSID)
            .ToListAsync();

        Assert.Single(tenant1Customers);
        Assert.Equal(customer1.FacebookPSID, tenant1Customers[0].FacebookPSID);

        dbContext.ChangeTracker.Clear();
        tenantContext.Initialize(tenant2Id, "page2", null);
        var tenant2Customers = await dbContext.CustomerIdentities
            .Where(c => c.FacebookPSID == customer1.FacebookPSID || c.FacebookPSID == customer2.FacebookPSID)
            .ToListAsync();

        Assert.Single(tenant2Customers);
        Assert.Equal(customer2.FacebookPSID, tenant2Customers[0].FacebookPSID);
    }

    [Fact]
    public async Task DraftOrders_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.Clear();
        var order1 = new DraftOrder
        {
            TenantId = tenant1Id,
            DraftCode = $"DR-T1-{Guid.NewGuid():N}",
            FacebookPSID = "psid1",
            FacebookPageId = "page1",
            CustomerPhone = "0901234567",
            ShippingAddress = "Address 1"
        };

        var order2 = new DraftOrder
        {
            TenantId = tenant2Id,
            DraftCode = $"DR-T2-{Guid.NewGuid():N}",
            FacebookPSID = "psid2",
            FacebookPageId = "page2",
            CustomerPhone = "0907654321",
            ShippingAddress = "Address 2"
        };

        dbContext.DraftOrders.AddRange(order1, order2);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Orders = await dbContext.DraftOrders
            .Where(o => o.DraftCode == order1.DraftCode || o.DraftCode == order2.DraftCode)
            .ToListAsync();

        Assert.Single(tenant1Orders);
        Assert.Equal(order1.DraftCode, tenant1Orders[0].DraftCode);

        dbContext.ChangeTracker.Clear();
        tenantContext.Initialize(tenant2Id, "page2", null);
        var tenant2Orders = await dbContext.DraftOrders
            .Where(o => o.DraftCode == order1.DraftCode || o.DraftCode == order2.DraftCode)
            .ToListAsync();

        Assert.Single(tenant2Orders);
        Assert.Equal(order2.DraftCode, tenant2Orders[0].DraftCode);
    }

    [Fact]
    public async Task ConversationSessions_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.Clear();
        var session1 = new ConversationSession
        {
            TenantId = tenant1Id,
            FacebookPSID = $"psid_s1_{Guid.NewGuid():N}",
            FacebookPageId = "page1",
            CurrentState = ConversationState.Idle,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        var session2 = new ConversationSession
        {
            TenantId = tenant2Id,
            FacebookPSID = $"psid_s2_{Guid.NewGuid():N}",
            FacebookPageId = "page2",
            CurrentState = ConversationState.Idle,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        dbContext.ConversationSessions.AddRange(session1, session2);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Sessions = await dbContext.ConversationSessions
            .Where(s => s.FacebookPSID == session1.FacebookPSID || s.FacebookPSID == session2.FacebookPSID)
            .ToListAsync();

        Assert.Single(tenant1Sessions);
        Assert.Equal(session1.FacebookPSID, tenant1Sessions[0].FacebookPSID);

        dbContext.ChangeTracker.Clear();
        tenantContext.Initialize(tenant2Id, "page2", null);
        var tenant2Sessions = await dbContext.ConversationSessions
            .Where(s => s.FacebookPSID == session1.FacebookPSID || s.FacebookPSID == session2.FacebookPSID)
            .ToListAsync();

        Assert.Single(tenant2Sessions);
        Assert.Equal(session2.FacebookPSID, tenant2Sessions[0].FacebookPSID);
    }

    [Fact]
    public async Task VipProfiles_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.Clear();
        var customer1 = new CustomerIdentity
        {
            TenantId = tenant1Id,
            FacebookPSID = $"psid_vip1_{Guid.NewGuid():N}",
            FacebookPageId = "page1"
        };

        var customer2 = new CustomerIdentity
        {
            TenantId = tenant2Id,
            FacebookPSID = $"psid_vip2_{Guid.NewGuid():N}",
            FacebookPageId = "page2"
        };

        dbContext.CustomerIdentities.AddRange(customer1, customer2);
        await dbContext.SaveChangesAsync();

        var vip1 = new VipProfile
        {
            TenantId = tenant1Id,
            CustomerIdentityId = customer1.Id,
            IsVip = true,
            Tier = VipTier.Vip
        };

        var vip2 = new VipProfile
        {
            TenantId = tenant2Id,
            CustomerIdentityId = customer2.Id,
            IsVip = true,
            Tier = VipTier.Vip
        };

        dbContext.VipProfiles.AddRange(vip1, vip2);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Vips = await dbContext.VipProfiles
            .Where(v => v.CustomerIdentityId == customer1.Id || v.CustomerIdentityId == customer2.Id)
            .ToListAsync();

        Assert.Single(tenant1Vips);
        Assert.Equal(customer1.Id, tenant1Vips[0].CustomerIdentityId);

        dbContext.ChangeTracker.Clear();
        tenantContext.Initialize(tenant2Id, "page2", null);
        var tenant2Vips = await dbContext.VipProfiles
            .Where(v => v.CustomerIdentityId == customer1.Id || v.CustomerIdentityId == customer2.Id)
            .ToListAsync();

        Assert.Single(tenant2Vips);
        Assert.Equal(customer2.Id, tenant2Vips[0].CustomerIdentityId);
    }

    [Fact]
    public async Task RiskSignals_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.Clear();
        var customer1 = new CustomerIdentity
        {
            TenantId = tenant1Id,
            FacebookPSID = $"psid_risk1_{Guid.NewGuid():N}",
            FacebookPageId = "page1"
        };

        var customer2 = new CustomerIdentity
        {
            TenantId = tenant2Id,
            FacebookPSID = $"psid_risk2_{Guid.NewGuid():N}",
            FacebookPageId = "page2"
        };

        dbContext.CustomerIdentities.AddRange(customer1, customer2);
        await dbContext.SaveChangesAsync();

        var risk1 = new RiskSignal
        {
            TenantId = tenant1Id,
            CustomerIdentityId = customer1.Id,
            Score = 0.8m,
            Level = RiskLevel.High,
            Source = "test"
        };

        var risk2 = new RiskSignal
        {
            TenantId = tenant2Id,
            CustomerIdentityId = customer2.Id,
            Score = 0.2m,
            Level = RiskLevel.Low,
            Source = "test"
        };

        dbContext.RiskSignals.AddRange(risk1, risk2);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Risks = await dbContext.RiskSignals
            .Where(r => r.CustomerIdentityId == customer1.Id || r.CustomerIdentityId == customer2.Id)
            .ToListAsync();

        Assert.Single(tenant1Risks);
        Assert.Equal(RiskLevel.High, tenant1Risks[0].Level);

        dbContext.ChangeTracker.Clear();
        tenantContext.Initialize(tenant2Id, "page2", null);
        var tenant2Risks = await dbContext.RiskSignals
            .Where(r => r.CustomerIdentityId == customer1.Id || r.CustomerIdentityId == customer2.Id)
            .ToListAsync();

        Assert.Single(tenant2Risks);
        Assert.Equal(RiskLevel.Low, tenant2Risks[0].Level);
    }
}
