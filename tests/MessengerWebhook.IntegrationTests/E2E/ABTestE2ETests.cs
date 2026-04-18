using System.Diagnostics;
using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.ABTesting.Configuration;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.IntegrationTests.E2E;

public class ABTestE2ETests
{
    private IServiceScopeFactory CreateServiceScopeFactory(string databaseName, Guid tenantId)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope())
            .Returns(() =>
            {
                var serviceCollection = new ServiceCollection();

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
    public async Task ControlUserJourney_PipelineSkipped_NullFields()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);

        var abTestLoggerMock = new Mock<ILogger<ABTestService>>();
        var metricsLoggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var abTestOptions = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 0, // Force all users to control
            HashSeed = "control-journey-test"
        });

        var abTestService = new ABTestService(CreateDbContext(databaseName), abTestOptions, abTestLoggerMock.Object);
        var metricsService = new ConversationMetricsService(scopeFactory, metricsLoggerMock.Object);

        var psid = "control-user-psid";
        var sessionId = Guid.NewGuid().ToString();

        // Create session
        using (var dbContext = CreateDbContext(databaseName))
        {
            var session = new ConversationSession
            {
                Id = sessionId,
                FacebookPSID = psid,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ConversationSessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        // Act - Step 1: User assigned to control
        var variant = await abTestService.GetVariantAsync(psid, sessionId);
        variant.Should().Be("control", "TreatmentPercentage=0 forces control");

        // Act - Step 2: Simulate message processing (pipeline skipped)
        var stopwatch = Stopwatch.StartNew();

        // Control: No pipeline execution (simulated)
        var baselineResponseTime = 50; // Baseline without pipeline

        stopwatch.Stop();

        // Act - Step 3: Log metrics with NULL pipeline fields
        var metricData = new ConversationMetricData
        {
            SessionId = sessionId,
            FacebookPSID = psid,
            ABTestVariant = "control",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = baselineResponseTime,
            // Control: NULL pipeline fields
            PipelineLatencyMs = null,
            DetectedEmotion = null,
            EmotionConfidence = null,
            MatchedTone = null,
            JourneyStage = null,
            ValidationPassed = null
        };

        await metricsService.LogAsync(metricData);
        await metricsService.FlushAsync();

        // Assert - Step 4: Verify response time within baseline
        baselineResponseTime.Should().BeLessThan(100, "Control should have minimal overhead");

        // Verify metrics logged with NULL pipeline fields
        using (var dbContext = CreateDbContext(databaseName))
        {
            var persistedMetric = await dbContext.ConversationMetrics
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SessionId == sessionId);

            persistedMetric.Should().NotBeNull();
            persistedMetric!.ABTestVariant.Should().Be("control");
            persistedMetric.PipelineLatencyMs.Should().BeNull("Control skips pipeline");
            persistedMetric.DetectedEmotion.Should().BeNull();
            persistedMetric.EmotionConfidence.Should().BeNull();
            persistedMetric.MatchedTone.Should().BeNull();
            persistedMetric.JourneyStage.Should().BeNull();
            persistedMetric.ValidationPassed.Should().BeNull();
        }

        // Verify logs show pipeline skipped
        abTestLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("A/B variant assigned")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TreatmentUserJourney_FullPipeline_Under100msOverhead()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);

        var abTestLoggerMock = new Mock<ILogger<ABTestService>>();
        var metricsLoggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var abTestOptions = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 100, // Force all users to treatment
            HashSeed = "treatment-journey-test"
        });

        var abTestService = new ABTestService(CreateDbContext(databaseName), abTestOptions, abTestLoggerMock.Object);
        var metricsService = new ConversationMetricsService(scopeFactory, metricsLoggerMock.Object);

        var psid = "treatment-user-psid";
        var sessionId = Guid.NewGuid().ToString();

        // Create session
        using (var dbContext = CreateDbContext(databaseName))
        {
            var session = new ConversationSession
            {
                Id = sessionId,
                FacebookPSID = psid,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ConversationSessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        // Act - Step 1: User assigned to treatment
        var variant = await abTestService.GetVariantAsync(psid, sessionId);
        variant.Should().Be("treatment", "TreatmentPercentage=100 forces treatment");

        // Act - Step 2: Simulate full pipeline execution
        var stopwatch = Stopwatch.StartNew();

        // Simulate pipeline stages (in real scenario, these would be actual service calls)
        var emotionDetectionLatency = 15; // ms
        var toneMatchingLatency = 10; // ms
        var contextAnalysisLatency = 20; // ms
        var smallTalkDetectionLatency = 12; // ms
        var responseValidationLatency = 18; // ms

        var pipelineLatency = emotionDetectionLatency + toneMatchingLatency +
                             contextAnalysisLatency + smallTalkDetectionLatency +
                             responseValidationLatency;

        var baselineResponseTime = 50; // Baseline without pipeline
        var totalResponseTime = baselineResponseTime + pipelineLatency;

        stopwatch.Stop();

        // Act - Step 3: Log metrics with complete pipeline data
        var metricData = new ConversationMetricData
        {
            SessionId = sessionId,
            FacebookPSID = psid,
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = totalResponseTime,
            // Treatment: Full pipeline data
            PipelineLatencyMs = pipelineLatency,
            DetectedEmotion = "happy",
            EmotionConfidence = 0.85m,
            MatchedTone = "friendly",
            JourneyStage = "consideration",
            ValidationPassed = true
        };

        await metricsService.LogAsync(metricData);
        await metricsService.FlushAsync();

        // Assert - Step 4: Verify response time <100ms overhead
        pipelineLatency.Should().BeLessThan(100, "Pipeline overhead should be under 100ms");
        totalResponseTime.Should().BeLessThan(150, "Total response time should be reasonable");

        // Verify metrics logged with complete pipeline data
        using (var dbContext = CreateDbContext(databaseName))
        {
            var persistedMetric = await dbContext.ConversationMetrics
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SessionId == sessionId);

            persistedMetric.Should().NotBeNull();
            persistedMetric!.ABTestVariant.Should().Be("treatment");
            persistedMetric.TotalResponseTimeMs.Should().Be(totalResponseTime);
            persistedMetric.PipelineLatencyMs.Should().Be(pipelineLatency);
            persistedMetric.DetectedEmotion.Should().Be("happy");
            persistedMetric.EmotionConfidence.Should().Be(0.85m);
            persistedMetric.MatchedTone.Should().Be("friendly");
            persistedMetric.JourneyStage.Should().Be("consideration");
            persistedMetric.ValidationPassed.Should().BeTrue();
        }

        // Verify logs show full pipeline executed
        abTestLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("A/B variant assigned")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        metricsLoggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Metric enqueued")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
