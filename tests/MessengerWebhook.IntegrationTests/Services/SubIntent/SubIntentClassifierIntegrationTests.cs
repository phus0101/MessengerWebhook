using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services.SubIntent;

/// <summary>
/// Integration tests for SubIntent classification - verifies real Gemini API calls
/// </summary>
public class SubIntentClassifierIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly SubIntentOptions _subIntentOptions;
    private readonly ILogger<GeminiSubIntentClassifier> _logger;

    public SubIntentClassifierIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _geminiOptions = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>()
            ?? throw new InvalidOperationException("Gemini configuration not found");

        _subIntentOptions = configuration.GetSection(SubIntentOptions.SectionName).Get<SubIntentOptions>()
            ?? throw new InvalidOperationException("SubIntent configuration not found");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
            Timeout = TimeSpan.FromSeconds(_geminiOptions.TimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _geminiOptions.ApiKey);

        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<GeminiSubIntentClassifier>();
    }

    [Fact(Skip = "Integration test - requires valid Gemini API key")]
    [Trait("Category", "Integration")]
    public async Task RealGeminiApi_VietnameseProductQuestion_ReturnsCorrectCategory()
    {
        var keywordDetector = new KeywordSubIntentDetector();
        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_subIntentOptions), _logger);
        var classifier = new HybridSubIntentClassifier(keywordDetector, aiClassifier, Options.Create(_subIntentOptions), LoggerFactory.Create(b => b.AddConsole()).CreateLogger<HybridSubIntentClassifier>());

        var result = await classifier.ClassifyAsync("sản phẩm này có chứa paraben không?");

        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.ProductQuestion);
        result.Confidence.Should().BeGreaterThan(0.5m);
    }

    [Theory(Skip = "Integration test - requires valid Gemini API key")]
    [InlineData("giá bao nhiêu?", SubIntentCategory.PriceQuestion)]
    [InlineData("ship mất bao lâu?", SubIntentCategory.ShippingQuestion)]
    [InlineData("còn hàng không?", SubIntentCategory.AvailabilityQuestion)]
    [Trait("Category", "Integration")]
    public async Task RealGeminiApi_VariousQuestions_ReturnsCorrectCategories(string message, SubIntentCategory expected)
    {
        var keywordDetector = new KeywordSubIntentDetector();
        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_subIntentOptions), _logger);
        var classifier = new HybridSubIntentClassifier(keywordDetector, aiClassifier, Options.Create(_subIntentOptions), LoggerFactory.Create(b => b.AddConsole()).CreateLogger<HybridSubIntentClassifier>());

        var result = await classifier.ClassifyAsync(message);

        result.Should().NotBeNull();
        result!.Category.Should().Be(expected);
    }

    [Fact(Skip = "Integration test - requires valid Gemini API key")]
    [Trait("Category", "Integration")]
    public async Task HybridClassifier_KeywordHighConfidence_FastPath()
    {
        var keywordDetector = new KeywordSubIntentDetector();
        var aiClassifier = new GeminiSubIntentClassifier(_httpClient, Options.Create(_geminiOptions), Options.Create(_subIntentOptions), _logger);
        var classifier = new HybridSubIntentClassifier(keywordDetector, aiClassifier, Options.Create(_subIntentOptions), LoggerFactory.Create(b => b.AddConsole()).CreateLogger<HybridSubIntentClassifier>());

        var result = await classifier.ClassifyAsync("giá bao nhiêu và có giảm giá không?");

        result.Should().NotBeNull();
        result!.Category.Should().Be(SubIntentCategory.PriceQuestion);
        result.Source.Should().Be("keyword");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
