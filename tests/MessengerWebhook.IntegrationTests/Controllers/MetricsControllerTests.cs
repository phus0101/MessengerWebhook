using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Metrics.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.Controllers;

public class MetricsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MetricsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private async Task<MessengerBotDbContext> GetDbContextAsync(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private Guid GetAuthorizedTenantId() => _factory.PrimaryTenantId;

    private async Task LoginAsync()
    {
        var preAuthResponse = await _client.GetAsync("/admin/api/auth/me");
        preAuthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var preAuthPayload = await preAuthResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var csrfToken = preAuthPayload.GetProperty("antiForgeryToken").GetString();
        csrfToken.Should().NotBeNullOrWhiteSpace();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = _factory.PrimaryManagerEmail,
                password = _factory.AdminPassword,
                rememberMe = true
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        var loginResponse = await _client.SendAsync(request);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSummary_ReturnsCorrectData()
    {
        // Arrange
        await LoginAsync();
        var testId = Guid.NewGuid().ToString("N");

        using var scope = _factory.Services.CreateScope();
        var dbContext = await GetDbContextAsync(scope);
        var tenantId = GetAuthorizedTenantId();

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Seed sessions first (required for foreign key)
        var sessions = new[]
        {
            new ConversationSession
            {
                Id = $"session-1-{testId}",
                FacebookPSID = $"psid-1-{testId}",
                TenantId = tenantId,
                ABTestVariant = "control",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationSession
            {
                Id = $"session-2-{testId}",
                FacebookPSID = $"psid-2-{testId}",
                TenantId = tenantId,
                ABTestVariant = "treatment",
                CreatedAt = startDate.AddDays(1)
            }
        };
        await dbContext.ConversationSessions.AddRangeAsync(sessions);
        await dbContext.SaveChangesAsync();

        // Seed test data
        var metrics = new[]
        {
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-1-{testId}",
                FacebookPSID = $"psid-1-{testId}",
                ABTestVariant = "control",
                MessageTimestamp = startDate.AddDays(1),
                ConversationTurn = 1,
                TotalResponseTimeMs = 100,
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-2-{testId}",
                FacebookPSID = $"psid-2-{testId}",
                ABTestVariant = "treatment",
                MessageTimestamp = startDate.AddDays(2),
                ConversationTurn = 1,
                TotalResponseTimeMs = 150,
                PipelineLatencyMs = 80,
                CreatedAt = startDate.AddDays(1)
            }
        };

        await dbContext.ConversationMetrics.AddRangeAsync(metrics);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync(
            $"/admin/api/metrics/summary?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<MetricsSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalConversations.Should().Be(2);
        summary.AvgMessagesPerConversation.Should().Be(1);
        summary.AvgPipelineLatencyMs.Should().Be(80);
    }

    [Fact]
    public async Task GetVariants_ComparesControlVsTreatment()
    {
        // Arrange
        await LoginAsync();
        var testId = Guid.NewGuid().ToString("N");

        using var scope = _factory.Services.CreateScope();
        var dbContext = await GetDbContextAsync(scope);
        var tenantId = GetAuthorizedTenantId();

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Seed sessions first (required for foreign key)
        var sessions = new[]
        {
            new ConversationSession
            {
                Id = $"session-control-1-{testId}",
                FacebookPSID = $"psid-1-{testId}",
                TenantId = tenantId,
                ABTestVariant = "control",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationSession
            {
                Id = $"session-control-2-{testId}",
                FacebookPSID = $"psid-2-{testId}",
                TenantId = tenantId,
                ABTestVariant = "control",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationSession
            {
                Id = $"session-treatment-1-{testId}",
                FacebookPSID = $"psid-3-{testId}",
                TenantId = tenantId,
                ABTestVariant = "treatment",
                CreatedAt = startDate.AddDays(1)
            }
        };
        await dbContext.ConversationSessions.AddRangeAsync(sessions);
        await dbContext.SaveChangesAsync();

        // Seed control and treatment metrics
        var metrics = new[]
        {
            // Control metrics
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-control-1-{testId}",
                FacebookPSID = $"psid-1-{testId}",
                ABTestVariant = "control",
                MessageTimestamp = startDate.AddDays(1),
                ConversationTurn = 1,
                TotalResponseTimeMs = 100,
                ConversationOutcome = "completed",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-control-2-{testId}",
                FacebookPSID = $"psid-2-{testId}",
                ABTestVariant = "control",
                MessageTimestamp = startDate.AddDays(2),
                ConversationTurn = 1,
                TotalResponseTimeMs = 110,
                ConversationOutcome = "escalated",
                CreatedAt = startDate.AddDays(1)
            },
            // Treatment metrics
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-treatment-1-{testId}",
                FacebookPSID = $"psid-3-{testId}",
                ABTestVariant = "treatment",
                MessageTimestamp = startDate.AddDays(1),
                ConversationTurn = 1,
                TotalResponseTimeMs = 150,
                PipelineLatencyMs = 80,
                DetectedEmotion = "happy",
                EmotionConfidence = 0.85m,
                MatchedTone = "friendly",
                ValidationPassed = true,
                ConversationOutcome = "completed",
                CreatedAt = startDate.AddDays(1)
            }
        };

        await dbContext.ConversationMetrics.AddRangeAsync(metrics);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync(
            $"/admin/api/metrics/variants?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var comparison = await response.Content.ReadFromJsonAsync<VariantComparisonDto>();
        comparison.Should().NotBeNull();

        comparison!.Control.TotalConversations.Should().Be(2);
        comparison.Control.CompletionRate.Should().Be(0.5m);
        comparison.Control.EscalationRate.Should().Be(0.5m);
        comparison.Control.AvgMessagesPerConversation.Should().Be(1);

        comparison.Treatment.TotalConversations.Should().Be(1);
        comparison.Treatment.CompletionRate.Should().Be(1.0m);
        comparison.Treatment.AvgPipelineLatencyMs.Should().Be(80);
        comparison.StatisticalSignificance.Should().BeFalse();
    }

    [Fact]
    public async Task GetPipeline_ShowsPerformanceBreakdown()
    {
        // Arrange
        await LoginAsync();
        var testId = Guid.NewGuid().ToString("N");

        using var scope = _factory.Services.CreateScope();
        var dbContext = await GetDbContextAsync(scope);
        var tenantId = GetAuthorizedTenantId();

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Seed sessions first (required for foreign key)
        var sessions = new[]
        {
            new ConversationSession
            {
                Id = $"session-1-{testId}",
                FacebookPSID = $"psid-1-{testId}",
                TenantId = tenantId,
                ABTestVariant = "treatment",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationSession
            {
                Id = $"session-2-{testId}",
                FacebookPSID = $"psid-2-{testId}",
                TenantId = tenantId,
                ABTestVariant = "treatment",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationSession
            {
                Id = $"session-3-{testId}",
                FacebookPSID = $"psid-3-{testId}",
                TenantId = tenantId,
                ABTestVariant = "treatment",
                CreatedAt = startDate.AddDays(1)
            }
        };
        await dbContext.ConversationSessions.AddRangeAsync(sessions);
        await dbContext.SaveChangesAsync();

        // Seed treatment metrics with pipeline latency
        var metrics = new[]
        {
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-1-{testId}",
                FacebookPSID = $"psid-1-{testId}",
                ABTestVariant = "treatment",
                MessageTimestamp = startDate.AddDays(1),
                ConversationTurn = 1,
                TotalResponseTimeMs = 150,
                PipelineLatencyMs = 80,
                AdditionalMetrics = JsonDocument.Parse("""{"emotionDetectionMs":20,"toneMatchingMs":10,"contextAnalysisMs":15,"smallTalkDetectionMs":5,"responseValidationMs":8}"""),
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-2-{testId}",
                FacebookPSID = $"psid-2-{testId}",
                ABTestVariant = "treatment",
                MessageTimestamp = startDate.AddDays(2),
                ConversationTurn = 1,
                TotalResponseTimeMs = 160,
                PipelineLatencyMs = 90,
                AdditionalMetrics = JsonDocument.Parse("""{"emotionDetectionMs":30,"toneMatchingMs":15,"contextAnalysisMs":20,"smallTalkDetectionMs":6,"responseValidationMs":9}"""),
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = $"session-3-{testId}",
                FacebookPSID = $"psid-3-{testId}",
                ABTestVariant = "treatment",
                MessageTimestamp = startDate.AddDays(3),
                ConversationTurn = 1,
                TotalResponseTimeMs = 170,
                PipelineLatencyMs = 100,
                AdditionalMetrics = JsonDocument.Parse("""{"emotionDetectionMs":40,"toneMatchingMs":20,"contextAnalysisMs":25,"smallTalkDetectionMs":7,"responseValidationMs":10}"""),
                CreatedAt = startDate.AddDays(1)
            }
        };

        await dbContext.ConversationMetrics.AddRangeAsync(metrics);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync(
            $"/admin/api/metrics/pipeline?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var performance = await response.Content.ReadFromJsonAsync<PipelinePerformanceDto>();
        performance.Should().NotBeNull();
        performance!.Total.P50.Should().Be(90, "P50 of [80, 90, 100] is 90");
        performance.Total.P95.Should().Be(99, "P95 percentile uses interpolation for [80, 90, 100]");
        performance.Total.P99.Should().Be(99, "P99 percentile uses interpolation for [80, 90, 100]");
    }

    [Fact]
    public async Task GetSummary_RequiresAuthentication()
    {
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        var response = await _client.GetAsync(
            $"/admin/api/metrics/summary?startDate={startDate:O}&endDate={endDate:O}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Endpoint requires authorization, should return 401 when no auth provided");
    }

    [Fact]
    public async Task GetSummary_AuthenticatedUser_SeesOnlyOwnTenantData()
    {
        await LoginAsync();
        var primaryTestId = Guid.NewGuid().ToString("N");
        var secondaryTestId = Guid.NewGuid().ToString("N");

        using var scope = _factory.Services.CreateScope();
        var dbContext = await GetDbContextAsync(scope);

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        await dbContext.ConversationSessions.AddRangeAsync(
            new ConversationSession
            {
                Id = $"session-primary-{primaryTestId}",
                FacebookPSID = $"psid-primary-{primaryTestId}",
                TenantId = _factory.PrimaryTenantId,
                ABTestVariant = "control",
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationSession
            {
                Id = $"session-secondary-{secondaryTestId}",
                FacebookPSID = $"psid-secondary-{secondaryTestId}",
                TenantId = _factory.SecondaryTenantId,
                ABTestVariant = "control",
                CreatedAt = startDate.AddDays(1)
            });
        await dbContext.SaveChangesAsync();

        await dbContext.ConversationMetrics.AddRangeAsync(
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = _factory.PrimaryTenantId,
                SessionId = $"session-primary-{primaryTestId}",
                FacebookPSID = $"psid-primary-{primaryTestId}",
                ABTestVariant = "control",
                MessageTimestamp = startDate.AddDays(1),
                ConversationTurn = 1,
                TotalResponseTimeMs = 100,
                CreatedAt = startDate.AddDays(1)
            },
            new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = _factory.SecondaryTenantId,
                SessionId = $"session-secondary-{secondaryTestId}",
                FacebookPSID = $"psid-secondary-{secondaryTestId}",
                ABTestVariant = "control",
                MessageTimestamp = startDate.AddDays(1),
                ConversationTurn = 1,
                TotalResponseTimeMs = 999,
                CreatedAt = startDate.AddDays(1)
            });
        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync(
            $"/admin/api/metrics/summary?startDate={startDate:O}&endDate={endDate:O}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<MetricsSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalConversations.Should().Be(1);
        summary.AvgMessagesPerConversation.Should().Be(1);
    }
}
