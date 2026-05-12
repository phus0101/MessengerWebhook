using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.AI.Strategies;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Options;
using Pinecone;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class AiServicesRegistration
{
    internal static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddSingleton<IValidateOptions<GeminiOptions>, ValidateGeminiOptions>();
        services.AddOptions<GeminiOptions>().ValidateOnStart();

        services.Configure<VertexAIOptions>(configuration.GetSection("VertexAI"));
        services.Configure<PineconeOptions>(configuration.GetSection("Pinecone"));

        // AI handlers and selection strategy
        services.AddSingleton<IModelSelectionStrategy, HybridModelSelectionStrategy>();
        services.AddTransient<GeminiAuthHandler>();
        services.AddTransient<GeminiRetryHandler>();

        // Gemini HttpClient with auth + retry handlers
        services.AddHttpClient<IGeminiService, GeminiService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            })
            .AddHttpMessageHandler<GeminiAuthHandler>()
            .AddHttpMessageHandler<GeminiRetryHandler>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

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
