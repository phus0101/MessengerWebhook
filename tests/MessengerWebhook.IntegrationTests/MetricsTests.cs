using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MessengerWebhook.IntegrationTests;

public class MetricsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MetricsTests(CustomWebApplicationFactory factory)
    {
        factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Metrics_ReturnsQueueMetrics()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var metrics = JsonSerializer.Deserialize<JsonElement>(content);

        metrics.GetProperty("queue_depth").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        metrics.GetProperty("queue_capacity").GetInt32().Should().Be(1000);
        metrics.GetProperty("queue_utilization_percent").GetDouble().Should().BeInRange(0, 100);
        metrics.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Health_ReturnsDetailedStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var health = JsonSerializer.Deserialize<JsonElement>(content);

        health.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
        health.GetProperty("checks").GetArrayLength().Should().BeGreaterThan(0);
        health.TryGetProperty("totalDuration", out _).Should().BeTrue();
    }
}
