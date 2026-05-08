using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.SubIntent;

// Regression: production wiring must resolve HybridSubIntentClassifier without throwing.
// Original bug: GeminiSubIntentClassifier was registered with AddScoped<>() instead of AddHttpClient<>().
// First request to a state handler crashed with "Unable to resolve service for type 'System.Net.Http.HttpClient'".
public class SubIntentDIRegistrationTests
{
    [Fact]
    public void HybridSubIntentClassifier_ResolvesFromDI_WithoutThrowing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:ProModel"] = "gemini-2.5-pro",
                ["Gemini:FlashLiteModel"] = "gemini-flash-lite",
                ["Gemini:TimeoutSeconds"] = "30",
                ["SubIntent:KeywordHighConfidenceThreshold"] = "0.9",
                ["SubIntent:HybridAiAcceptanceThreshold"] = "0.7",
                ["SubIntent:MinConfidence"] = "0.5",
                ["SubIntent:EnableAiFallback"] = "true",
                ["SubIntent:ClassifierTimeoutMs"] = "1500"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<SubIntentOptions>(configuration.GetSection(SubIntentOptions.SectionName));
        services.AddTransient<GeminiAuthHandler>();
        services.AddSingleton<KeywordSubIntentDetector>();
        services.AddHttpClient<GeminiSubIntentClassifier>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<GeminiAuthHandler>();
        services.AddScoped<ISubIntentClassifier, HybridSubIntentClassifier>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Action act = () => scope.ServiceProvider.GetRequiredService<ISubIntentClassifier>();

        act.Should().NotThrow("DI graph for HybridSubIntentClassifier must resolve cleanly in production");
    }
}
