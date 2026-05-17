using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.AI.Resilience;
using MessengerWebhook.Services.AI.Routing;
using MessengerWebhook.Services.AI.Strategies;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Pinecone;
using Polly;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class AiServicesRegistration
{
    internal static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddSingleton<IValidateOptions<GeminiOptions>, ValidateGeminiOptions>();
        services.AddOptions<GeminiOptions>().ValidateOnStart();

        // LLM model tier routing
        services.Configure<LlmRoutingOptions>(configuration.GetSection(LlmRoutingOptions.SectionName));
        services.AddSingleton<ILlmRoutingService, LlmRoutingService>();

        services.Configure<VertexAIOptions>(configuration.GetSection("VertexAI"));
        services.Configure<PineconeOptions>(configuration.GetSection("Pinecone"));

        // AI handlers and selection strategy
        services.AddSingleton<IModelSelectionStrategy, HybridModelSelectionStrategy>();
        services.AddTransient<GeminiAuthHandler>();

        // Fallback service for degraded LLM responses when circuit is open
        services.AddScoped<ILlmFallbackService, LlmFallbackService>();

        // Conversation summarization for context-window compression
        services.AddScoped<IConversationSummarizer, ConversationSummarizer>();

        // Gemini HttpClient with auth handler + Polly resilience pipeline (circuit breaker + retry + timeout)
        services.AddHttpClient<IGeminiService, GeminiService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                // Outer timeout is managed by Polly; set HttpClient timeout higher to avoid races
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds + 5);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddHttpMessageHandler<GeminiAuthHandler>()
            .AddResilienceHandler("gemini-pipeline", pipeline =>
            {
                // 1. Circuit breaker: open after 50% failures over 30s window (min 5 requests)
                pipeline.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    OnOpened = args =>
                    {
                        return default;
                    },
                    OnClosed = args => default
                });

                // 2. Retry: up to 2 retries on 429/503 with exponential backoff
                pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode == 503)
                });

                // 3. Per-attempt timeout
                pipeline.AddTimeout(TimeSpan.FromSeconds(15));
            });

        // VertexAI embedding — concrete type registered first so cache wrapper can resolve it
        services.AddHttpClient<VertexAIEmbeddingService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<VertexAIOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });
        services.AddScoped<IEmbeddingService, VertexAIEmbeddingService>();

        // Pinecone client (thread-safe singleton)
        services.AddSingleton<PineconeClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PineconeOptions>>().Value;
            return new PineconeClient(opts.ApiKey);
        });

        // Vector search
        services.AddSingleton<IIndexingProgressTracker, IndexingProgressTracker>();
        services.AddScoped<IVectorSearchService, PineconeVectorService>();
        services.AddScoped<ProductEmbeddingPipeline>();

        // Hybrid search — concrete type registered first so cache wrapper can resolve it
        services.AddScoped<KeywordSearchService>();
        services.AddScoped<RRFFusionService>();
        services.AddScoped<HybridSearchService>();
        services.AddScoped<IHybridSearchService, HybridSearchService>();

        // RAG
        services.Configure<RAGOptions>(configuration.GetSection(RAGOptions.SectionName));
        services.AddScoped<IContextAssembler, ContextAssembler>();
        services.AddScoped<IRAGService, RAGService>();

        return services;
    }
}
