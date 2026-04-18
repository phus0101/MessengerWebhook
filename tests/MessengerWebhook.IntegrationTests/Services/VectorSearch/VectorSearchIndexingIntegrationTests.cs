using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MessengerWebhook.Data;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services.VectorSearch;

public class VectorSearchIndexingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public VectorSearchIndexingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetStateAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task IndexAll_ShouldReturn401_WhenNotAuthenticated()
    {
        // Act
        var response = await _client.PostAsync("/admin/api/vector-search/index-all", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IndexStatus_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/admin/api/vector-search/index-status/{jobId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IndexStatus_ShouldReturn404_WhenJobNotFound()
    {
        // Arrange
        await LoginAsync();
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/admin/api/vector-search/index-status/{nonExistentJobId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(content);
        Assert.Contains("not found or expired", content.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndexAll_ShouldCreateJobAndReturnJobId()
    {
        // Arrange
        var session = await LoginAsync();

        // Act
        var response = await PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IndexAllResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.Contains("background", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndexAll_ShouldReturn409Conflict_WhenJobAlreadyRunning()
    {
        // Arrange
        var session = await LoginAsync();

        // Start first job
        var firstResponse = await PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act - Try to start second job immediately
        var secondResponse = await PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var content = await secondResponse.Content.ReadFromJsonAsync<ConflictResponse>();
        Assert.NotNull(content);
        Assert.Contains("already running", content.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, content.JobId);
    }

    [Fact]
    public async Task IndexAll_ShouldAllowOnlyOneSuccessfulStart_WhenRequestsRace()
    {
        var session = await LoginAsync();

        var firstTask = PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });
        var secondTask = PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });

        var responses = await Task.WhenAll(firstTask, secondTask);
        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);
    }

    [Fact]
    public async Task IndexStatus_ShouldReturnJobProgress()
    {
        // Arrange
        var session = await LoginAsync();

        // Start indexing job
        var startResponse = await PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });
        var startResult = await startResponse.Content.ReadFromJsonAsync<IndexAllResponse>();
        Assert.NotNull(startResult);

        // Act - Check status immediately
        var statusResponse = await _client.GetAsync($"/admin/api/vector-search/index-status/{startResult.JobId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<IndexStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal(startResult.JobId, status.JobId);
        Assert.NotNull(status.Status);
        Assert.True(status.TotalProducts >= 0);
        Assert.True(status.IndexedProducts >= 0);
        Assert.True(status.ProgressPercentage >= 0 && status.ProgressPercentage <= 100);
        // StartedAt is DateTime (value type), always has a value
    }

    [Fact]
    public async Task IndexStatus_ShouldShowProgressUpdates()
    {
        // Arrange
        var session = await LoginAsync();

        // Start indexing job
        var startResponse = await PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });
        var startResult = await startResponse.Content.ReadFromJsonAsync<IndexAllResponse>();
        Assert.NotNull(startResult);

        // Act - Poll status multiple times
        var statuses = new List<IndexStatusResponse>();
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(500); // Wait for some progress
            var statusResponse = await _client.GetAsync($"/admin/api/vector-search/index-status/{startResult.JobId}");
            if (statusResponse.IsSuccessStatusCode)
            {
                var status = await statusResponse.Content.ReadFromJsonAsync<IndexStatusResponse>();
                if (status != null)
                {
                    statuses.Add(status);
                }
            }
        }

        // Assert - Should have captured some statuses
        Assert.NotEmpty(statuses);
        Assert.All(statuses, s => Assert.Equal(startResult.JobId, s.JobId));

        // Check if job completed or is still running
        var lastStatus = statuses.Last();
        Assert.True(
            lastStatus.Status == "Running" ||
            lastStatus.Status == "Completed" ||
            lastStatus.Status == "Failed");
    }

    [Fact]
    public async Task IndexProduct_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var productId = "test-product-123";

        // Act
        var response = await _client.PostAsync($"/admin/api/vector-search/index-product/{productId}", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IndexProduct_ShouldIndexSingleProduct()
    {
        // Arrange
        var session = await LoginAsync();

        // Get a product ID from the database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var product = dbContext.Products.FirstOrDefault();

        if (product == null)
        {
            // Skip test if no products available
            return;
        }

        // Act
        var response = await PostJsonAsync($"/admin/api/vector-search/index-product/{product.Id}", session.CsrfToken, new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IndexProductResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(product.Id, result.ProductId);
    }

    [Fact]
    public async Task JobLifecycle_ShouldTransitionFromRunningToCompleted()
    {
        // Arrange
        var session = await LoginAsync();

        // Act - Start job
        var startResponse = await PostJsonAsync("/admin/api/vector-search/index-all", session.CsrfToken, new { });
        var startResult = await startResponse.Content.ReadFromJsonAsync<IndexAllResponse>();
        Assert.NotNull(startResult);

        // Poll until completion or timeout
        var maxAttempts = 60; // 30 seconds max
        var attempt = 0;
        IndexStatusResponse? finalStatus = null;

        while (attempt < maxAttempts)
        {
            await Task.Delay(500);
            var statusResponse = await _client.GetAsync($"/admin/api/vector-search/index-status/{startResult.JobId}");

            if (statusResponse.IsSuccessStatusCode)
            {
                finalStatus = await statusResponse.Content.ReadFromJsonAsync<IndexStatusResponse>();

                if (finalStatus?.Status == "Completed" || finalStatus?.Status == "Failed")
                {
                    break;
                }
            }

            attempt++;
        }

        // Assert
        Assert.NotNull(finalStatus);
        Assert.True(finalStatus.Status == "Completed" || finalStatus.Status == "Failed");

        if (finalStatus.Status == "Completed")
        {
            Assert.NotNull(finalStatus.CompletedAt);
            Assert.Equal(100, finalStatus.ProgressPercentage);
            Assert.Null(finalStatus.CurrentProductId);
            Assert.Null(finalStatus.CurrentProductName);
        }
        else if (finalStatus.Status == "Failed")
        {
            Assert.NotNull(finalStatus.CompletedAt);
            Assert.NotNull(finalStatus.ErrorMessage);
        }
    }

    // Helper methods
    private async Task<AdminSession> LoginAsync()
    {
        var preAuth = await GetAuthStateAsync();
        var loginResponse = await PostJsonAsync(
            "/admin/api/auth/login",
            preAuth.CsrfToken,
            new { email = _factory.PrimaryManagerEmail, password = _factory.AdminPassword, rememberMe = true });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return await GetAuthStateAsync();
    }

    private async Task<AdminSession> GetAuthStateAsync()
    {
        var response = await _client.GetAsync("/admin/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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

    private async Task<HttpResponseMessage> PostJsonAsync<T>(string url, string csrfToken, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await _client.SendAsync(request);
    }

    // Response DTOs
    private record ErrorResponse(string Error);
    private record IndexAllResponse(bool Success, Guid JobId, string Message);
    private record ConflictResponse(string Error, Guid JobId);
    private record IndexProductResponse(bool Success, string ProductId);
    private record IndexStatusResponse(
        Guid JobId,
        string Status,
        int TotalProducts,
        int IndexedProducts,
        int ProgressPercentage,
        string? CurrentProductId,
        string? CurrentProductName,
        DateTime StartedAt,
        DateTime? CompletedAt,
        string? ErrorMessage);
}
