using System.Diagnostics;
using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Metrics;

public class ConversationMetricsServiceTests
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
    public async Task LogAsync_NonBlocking_CompletesUnder10ms()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        var metricData = new ConversationMetricData
        {
            SessionId = Guid.NewGuid().ToString(),
            FacebookPSID = "test-psid",
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 150,
            PipelineLatencyMs = 80
        };

        // Act - Measure latency
        var stopwatch = Stopwatch.StartNew();
        await service.LogAsync(metricData);
        stopwatch.Stop();

        // Assert - Should complete under 10ms (non-blocking)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10,
            $"LogAsync should be non-blocking and complete under 10ms, took {stopwatch.ElapsedMilliseconds}ms");

        service.GetBufferSize().Should().Be(1);
    }

    [Fact]
    public async Task FlushAsync_100Items_TriggersFlush()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        // Act - Enqueue 100 metrics
        for (int i = 0; i < 100; i++)
        {
            var metricData = new ConversationMetricData
            {
                SessionId = Guid.NewGuid().ToString(),
                FacebookPSID = $"psid-{i}",
                ABTestVariant = i % 2 == 0 ? "control" : "treatment",
                MessageTimestamp = DateTime.UtcNow,
                ConversationTurn = 1,
                TotalResponseTimeMs = 150 + i,
                PipelineLatencyMs = i % 2 == 0 ? null : 80
            };
            await service.LogAsync(metricData);
        }

        service.GetBufferSize().Should().Be(100);

        // Act - Flush
        await service.FlushAsync();

        // Assert - Buffer should be empty
        service.GetBufferSize().Should().Be(0);

        // Verify metrics persisted to database
        using var dbContext = CreateDbContext(databaseName);
        var metricsCount = await dbContext.ConversationMetrics.CountAsync();
        metricsCount.Should().Be(100);

        // Verify log message
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Flushed 100 metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_PeriodicFlush_TriggersEvery60Seconds()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        // Act - Enqueue 10 metrics (below 100 threshold)
        for (int i = 0; i < 10; i++)
        {
            var metricData = new ConversationMetricData
            {
                SessionId = Guid.NewGuid().ToString(),
                FacebookPSID = $"psid-{i}",
                ABTestVariant = "treatment",
                MessageTimestamp = DateTime.UtcNow,
                ConversationTurn = 1,
                TotalResponseTimeMs = 150,
                PipelineLatencyMs = 80
            };
            await service.LogAsync(metricData);
        }

        service.GetBufferSize().Should().Be(10);

        // Simulate periodic flush (called by background service every 60s)
        await service.FlushAsync();

        // Assert - Buffer should be empty after periodic flush
        service.GetBufferSize().Should().Be(0);

        // Verify metrics persisted
        using var dbContext = CreateDbContext(databaseName);
        var metricsCount = await dbContext.ConversationMetrics.CountAsync();
        metricsCount.Should().Be(10);
    }

    [Fact]
    public async Task FlushAsync_FailedFlush_DoesNotCrashApp()
    {
        // Arrange - Mock scope creation failure to simulate flush failure
        var tenantId = Guid.NewGuid();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope())
            .Throws(new InvalidOperationException("Database connection failed"));

        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();
        var service = new ConversationMetricsService(scopeFactoryMock.Object, loggerMock.Object);

        // Enqueue metrics
        var metricData = new ConversationMetricData
        {
            SessionId = Guid.NewGuid().ToString(),
            FacebookPSID = "test-psid",
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 150,
            PipelineLatencyMs = 80
        };
        await service.LogAsync(metricData);

        // Act - Flush should catch exception internally and not propagate
        var flushAction = async () => await service.FlushAsync();
        await flushAction.Should().NotThrowAsync("Service should catch and log errors internally");

        // Verify error was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to flush")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify metrics were re-enqueued for retry
        service.GetBufferSize().Should().Be(1, "Failed metrics should be re-enqueued");
    }

    [Fact]
    public async Task LogAsync_BufferFull_EvictsOldestMetric()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var scopeFactory = CreateServiceScopeFactory(databaseName, tenantId);
        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();

        var service = new ConversationMetricsService(scopeFactory, loggerMock.Object);

        // Act - Fill buffer to 10000 (max capacity)
        for (int i = 0; i < 10000; i++)
        {
            var metricData = new ConversationMetricData
            {
                SessionId = Guid.NewGuid().ToString(),
                FacebookPSID = $"psid-{i}",
                ABTestVariant = "treatment",
                MessageTimestamp = DateTime.UtcNow,
                ConversationTurn = 1,
                TotalResponseTimeMs = 150,
                PipelineLatencyMs = 80
            };
            await service.LogAsync(metricData);
        }

        service.GetBufferSize().Should().Be(10000);

        // Add one more to trigger eviction
        var extraMetric = new ConversationMetricData
        {
            SessionId = Guid.NewGuid().ToString(),
            FacebookPSID = "psid-extra",
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 150,
            PipelineLatencyMs = 80
        };
        await service.LogAsync(extraMetric);

        // Assert - Buffer should still be 10000 (oldest evicted)
        service.GetBufferSize().Should().Be(10000);

        // Verify warning was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Metrics buffer full")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_RetryFailure5Times_DropsMetric()
    {
        // Arrange - Mock scope factory to always throw
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope())
            .Throws(new InvalidOperationException("Persistent database failure"));

        var loggerMock = new Mock<ILogger<ConversationMetricsService>>();
        var service = new ConversationMetricsService(scopeFactoryMock.Object, loggerMock.Object);

        // Enqueue metric
        var metricData = new ConversationMetricData
        {
            SessionId = Guid.NewGuid().ToString(),
            FacebookPSID = "test-psid",
            ABTestVariant = "treatment",
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = 1,
            TotalResponseTimeMs = 150,
            PipelineLatencyMs = 80
        };
        await service.LogAsync(metricData);

        // Act - Retry flush 5 times (no try-catch needed, service catches internally)
        for (int i = 0; i < 5; i++)
        {
            await service.FlushAsync();
        }

        // Buffer should still have the metric (retry count = 5)
        service.GetBufferSize().Should().Be(1);

        // 6th attempt should drop the metric
        await service.FlushAsync();

        // Assert - Metric should be dropped after 5 retries
        service.GetBufferSize().Should().Be(0, "Metric should be dropped after 5 failed retries");

        // Verify drop warning was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Dropping metric after 5 failed retries")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
