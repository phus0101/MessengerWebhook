using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.ABTesting.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.IntegrationTests.Services;

public class ABTestIntegrationTests
{
    private MessengerBotDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new MessengerBotDbContext(options);
    }

    [Fact]
    public async Task GetVariantAsync_VariantPersistedToDatabase()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "integration-test"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        var psid = "integration-test-psid";
        var sessionId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create session in database
        var session = new ConversationSession
        {
            Id = sessionId,
            FacebookPSID = psid,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ConversationSessions.Add(session);
        await dbContext.SaveChangesAsync();

        // Clear change tracker
        dbContext.ChangeTracker.Clear();

        // Act - Get variant (should assign and persist)
        var variant = await service.GetVariantAsync(psid, sessionId);

        // Assert - Verify variant is persisted in database
        var persistedSession = await dbContext.ConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        persistedSession.Should().NotBeNull();
        persistedSession!.ABTestVariant.Should().Be(variant);
        persistedSession.ABTestVariant.Should().BeOneOf("treatment", "control");

        // Verify audit trail
        persistedSession.FacebookPSID.Should().Be(psid);
        persistedSession.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetVariantAsync_VariantConsistentAcrossMultipleMessages()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "consistency-test"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        var psid = "consistency-test-psid";
        var sessionId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create session
        var session = new ConversationSession
        {
            Id = sessionId,
            FacebookPSID = psid,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ConversationSessions.Add(session);
        await dbContext.SaveChangesAsync();

        // Clear change tracker
        dbContext.ChangeTracker.Clear();

        // Act - Simulate multiple messages in same session
        var variant1 = await service.GetVariantAsync(psid, sessionId);
        dbContext.ChangeTracker.Clear();

        var variant2 = await service.GetVariantAsync(psid, sessionId);
        dbContext.ChangeTracker.Clear();

        var variant3 = await service.GetVariantAsync(psid, sessionId);
        dbContext.ChangeTracker.Clear();

        // Assert - All calls return same variant
        variant1.Should().Be(variant2);
        variant2.Should().Be(variant3);

        // Verify database consistency
        var persistedSession = await dbContext.ConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        persistedSession!.ABTestVariant.Should().Be(variant1);

        // Verify no duplicate assignments
        var sessionCount = await dbContext.ConversationSessions
            .Where(s => s.FacebookPSID == psid)
            .CountAsync();

        sessionCount.Should().Be(1, "Should not create duplicate sessions");
    }

    [Fact]
    public async Task GetVariantAsync_TenantIsolation_DifferentTenantsIndependent()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "tenant-isolation-test"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        var psid = "tenant-isolation-psid";
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create sessions for same PSID but different tenants
        var session1 = new ConversationSession
        {
            Id = Guid.NewGuid().ToString(),
            FacebookPSID = psid,
            TenantId = tenant1Id,
            CreatedAt = DateTime.UtcNow
        };

        var session2 = new ConversationSession
        {
            Id = Guid.NewGuid().ToString(),
            FacebookPSID = psid,
            TenantId = tenant2Id,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ConversationSessions.AddRange(session1, session2);
        await dbContext.SaveChangesAsync();

        // Clear change tracker
        dbContext.ChangeTracker.Clear();

        // Act - Get variants for both tenants
        var variant1 = await service.GetVariantAsync(psid, session1.Id);
        dbContext.ChangeTracker.Clear();

        var variant2 = await service.GetVariantAsync(psid, session2.Id);

        // Assert - Both should get same variant (deterministic by PSID)
        variant1.Should().Be(variant2, "Same PSID should get same variant regardless of tenant");

        // Verify both sessions have variants persisted
        var persistedSessions = await dbContext.ConversationSessions
            .AsNoTracking()
            .Where(s => s.FacebookPSID == psid)
            .ToListAsync();

        persistedSessions.Should().HaveCount(2);
        persistedSessions.Should().AllSatisfy(s => s.ABTestVariant.Should().NotBeNullOrEmpty());
    }
}
