using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.IntegrationTests.Services;

public class MetricsCollectionIntegrationTests
{
    private IServiceScopeFactory CreateServiceScopeFactory(string databaseName, Guid tenantId)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope())
            .Returns(() =>
            {
                var serviceCollection = new ServiceCollection();

                // Create new DbContext for each scope
                var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
                    .UseInMemoryDatabase(databaseName: databaseName)
                    .Options;
                var dbContext = new MessengerBotDbContext(options);
                serviceCollection.AddScoped(_ => dbContext);

                var tenantContextMock = new Mock<ITenantContext>();
                tenantContextMock.Setup(x => x.TenantId).Returns(tenantId);
                serviceCollection.AddScoped(_ => tenantContextMock.Object);

                var serviceProvider = serviceCollection.BuildServiceProvider();
                return serviceProvider.CreateScope();
            });

        return scopeFactoryMock.Object;
    }

    private MessengerBotDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        return new MessengerBotDbContext(options);
    }

    [Fact]
    public async Task LogAsync_ControlMetrics_NullPipelineFields()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        var controlMetric = new ConversationMetricData
        {
            SessionId = Guid.NewGuid().ToString(),
            FacebookPSID = "control-psid",
            ABTestVariant = "control",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 100,
            // Control: No pipeline fields
            PipelineLatencyMs = null,
            DetectedEmotion = null,
            EmotionConfidence = null,
            MatchedTone = null,
            JourneyStage = null,
            ValidationPassed = null
        };

        // Act
        await service.LogAsync(controlMetric);
        await service.FlushAsync();

        // Assert - Verify control metrics have NULL pipeline fields
        using var dbContext = CreateDbContext(databaseName);
        var persistedMetric = await dbContext.ConversationMetrics
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.FacebookPSID == "control-psid");

        persistedMetric.Should().NotBeNull();
        persistedMetric!.ABTestVariant.Should().Be("control");
        persistedMetric.TotalResponseTimeMs.Should().Be(100);

        // Verify NULL pipeline fields
        persistedMetric.PipelineLatencyMs.Should().BeNull();
        persistedMetric.DetectedEmotion.Should().BeNull();
        persistedMetric.EmotionConfidence.Should().BeNull();
        persistedMetric.MatchedTone.Should().BeNull();
        persistedMetric.JourneyStage.Should().BeNull();
        persistedMetric.ValidationPassed.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_TreatmentMetrics_FullPipelineData()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        var treatmentMetric = new ConversationMetricData
        {
            SessionId = Guid.NewGuid().ToString(),
            FacebookPSID = "treatment-psid",
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 150,
            // Treatment: Full pipeline data
            PipelineLatencyMs = 80,
            DetectedEmotion = "happy",
            EmotionConfidence = 0.85m,
            MatchedTone = "friendly",
            JourneyStage = "consideration",
            ValidationPassed = true,
            ValidationErrors = null
        };

        // Act
        await service.LogAsync(treatmentMetric);
        await service.FlushAsync();

        // Assert - Verify treatment metrics have complete pipeline data
        using var dbContext = CreateDbContext(databaseName);
        var persistedMetric = await dbContext.ConversationMetrics
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.FacebookPSID == "treatment-psid");

        persistedMetric.Should().NotBeNull();
        persistedMetric!.ABTestVariant.Should().Be("treatment");
        persistedMetric.TotalResponseTimeMs.Should().Be(150);

        // Verify complete pipeline fields
        persistedMetric.PipelineLatencyMs.Should().Be(80);
        persistedMetric.DetectedEmotion.Should().Be("happy");
        persistedMetric.EmotionConfidence.Should().Be(0.85m);
        persistedMetric.MatchedTone.Should().Be("friendly");
        persistedMetric.JourneyStage.Should().Be("consideration");
        persistedMetric.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public async Task LogAsync_MetricsTiedToCorrectSession()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        var session1Id = Guid.NewGuid().ToString();
        var session2Id = Guid.NewGuid().ToString();
        var psid1 = "psid-session-1";
        var psid2 = "psid-session-2";

        // Create sessions in database
        using (var setupDbContext = CreateDbContext(databaseName))
        {
            var session1 = new ConversationSession
            {
                Id = session1Id,
                FacebookPSID = psid1,
                TenantId = tenantId,
                ABTestVariant = "control",
                CreatedAt = DateTime.UtcNow
            };

            var session2 = new ConversationSession
            {
                Id = session2Id,
                FacebookPSID = psid2,
                TenantId = tenantId,
                ABTestVariant = "treatment",
                CreatedAt = DateTime.UtcNow
            };

            setupDbContext.ConversationSessions.AddRange(session1, session2);
            await setupDbContext.SaveChangesAsync();
        }

        // Log metrics for both sessions
        var metric1 = new ConversationMetricData
        {
            SessionId = session1Id,
            FacebookPSID = psid1,
            ABTestVariant = "control",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 100
        };

        var metric2 = new ConversationMetricData
        {
            SessionId = session2Id,
            FacebookPSID = psid2,
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 150,
            PipelineLatencyMs = 80
        };

        // Act
        await service.LogAsync(metric1);
        await service.LogAsync(metric2);
        await service.FlushAsync();

        // Assert - Verify metrics are tied to correct sessions
        using var dbContext = CreateDbContext(databaseName);
        var session1Metrics = await dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.SessionId == session1Id)
            .ToListAsync();

        var session2Metrics = await dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.SessionId == session2Id)
            .ToListAsync();

        session1Metrics.Should().HaveCount(1);
        session1Metrics[0].FacebookPSID.Should().Be(psid1);
        session1Metrics[0].ABTestVariant.Should().Be("control");
        session1Metrics[0].TotalResponseTimeMs.Should().Be(100);

        session2Metrics.Should().HaveCount(1);
        session2Metrics[0].FacebookPSID.Should().Be(psid2);
        session2Metrics[0].ABTestVariant.Should().Be("treatment");
        session2Metrics[0].TotalResponseTimeMs.Should().Be(150);
        session2Metrics[0].PipelineLatencyMs.Should().Be(80);

        // Verify tenant isolation
        session1Metrics[0].TenantId.Should().Be(tenantId);
        session2Metrics[0].TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task LogAsync_MultipleMessagesInSession_AllPersisted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        var sessionId = Guid.NewGuid().ToString();
        var psid = "multi-message-psid";

        // Log 5 messages in same session
        for (int turn = 1; turn <= 5; turn++)
        {
            var metric = new ConversationMetricData
            {
                SessionId = sessionId,
                FacebookPSID = psid,
                ABTestVariant = "treatment",
                MessageTimestamp = DateTime.UtcNow.AddSeconds(turn),
                ConversationTurn = turn,
                TotalResponseTimeMs = 100 + (turn * 10),
                PipelineLatencyMs = 50 + (turn * 5)
            };
            await service.LogAsync(metric);
        }

        // Act
        await service.FlushAsync();

        // Assert - Verify all 5 messages persisted
        using var dbContext = CreateDbContext(databaseName);
        var sessionMetrics = await dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.ConversationTurn)
            .ToListAsync();

        sessionMetrics.Should().HaveCount(5);

        for (int i = 0; i < 5; i++)
        {
            var turn = i + 1;
            sessionMetrics[i].ConversationTurn.Should().Be(turn);
            sessionMetrics[i].TotalResponseTimeMs.Should().Be(100 + (turn * 10));
            sessionMetrics[i].PipelineLatencyMs.Should().Be(50 + (turn * 5));
        }
    }
}
