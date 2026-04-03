using System.Threading.Channels;
using DotNetEnv;
using MessengerWebhook.BackgroundServices;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Endpoints;
using MessengerWebhook.Middleware;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.AI.Strategies;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Knowledge;
using MessengerWebhook.Services.LiveComments;
using MessengerWebhook.Services.Nobita;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.QuickReply;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Support.EmailTemplates;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using MessengerWebhook.Services.Cache;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Pinecone;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Load .env file in Development
if (builder.Environment.IsDevelopment())
{
    Env.Load();

    // Map environment variables to configuration
    var pineconeApiKey = Environment.GetEnvironmentVariable("PINECONE_API_KEY");
    if (!string.IsNullOrWhiteSpace(pineconeApiKey))
    {
        builder.Configuration["Pinecone:ApiKey"] = pineconeApiKey;
    }
}

// Use Serilog for logging
builder.Host.UseSerilog();

// Configure strongly-typed options
builder.Services.Configure<FacebookOptions>(
    builder.Configuration.GetSection(FacebookOptions.SectionName));
builder.Services.Configure<WebhookOptions>(
    builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<NobitaOptions>(
    builder.Configuration.GetSection(NobitaOptions.SectionName));
builder.Services.Configure<SupportOptions>(
    builder.Configuration.GetSection(SupportOptions.SectionName));
builder.Services.Configure<SalesBotOptions>(
    builder.Configuration.GetSection(SalesBotOptions.SectionName));
builder.Services.Configure<AdminOptions>(
    builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<LiveCommentOptions>(
    builder.Configuration.GetSection(LiveCommentOptions.SectionName));
builder.Services.Configure<VertexAIOptions>(
    builder.Configuration.GetSection("VertexAI"));
builder.Services.Configure<PineconeOptions>(
    builder.Configuration.GetSection("Pinecone"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        var adminOptions = builder.Configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions();
        options.Cookie.Name = adminOptions.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = adminOptions.LoginPath;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/admin/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/admin/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// Configure PostgreSQL DbContext with pgvector
builder.Services.AddDbContext<MessengerBotDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()));

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<MessengerWebhook.HealthChecks.ChannelHealthCheck>("channel_queue")
    .AddCheck<MessengerWebhook.HealthChecks.GraphApiHealthCheck>("graph_api");

// Configure JSON serializer for case-insensitive property matching
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Add memory cache for idempotency with size limit
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100_000; // Limit to 100k entries
    options.CompactionPercentage = 0.25; // Evict 25% when full
});

// Register services
builder.Services.AddSingleton<ISignatureValidator, SignatureValidator>();
builder.Services.AddScoped<WebhookProcessor>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IPolicyGuardService, PolicyGuardService>();
builder.Services.AddScoped<IRiskMessageSanitizer, RiskMessageSanitizer>();
builder.Services.AddScoped<IBotLockService, BotLockService>();
builder.Services.AddScoped<ICaseEscalationService, CaseEscalationService>();
builder.Services.AddScoped<IKnowledgeImportService, KnowledgeImportService>();
builder.Services.AddScoped<ILiveCommentAutomationService, LiveCommentAutomationService>();
builder.Services.AddScoped<ICustomerIntelligenceService, CustomerIntelligenceService>();
builder.Services.AddScoped<IDraftOrderService, DraftOrderService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
builder.Services.AddScoped<IAdminDashboardQueryService, AdminDashboardQueryService>();
builder.Services.AddScoped<IAdminDraftOrderService, AdminDraftOrderService>();
builder.Services.AddScoped<INobitaSubmissionService, NobitaSubmissionService>();
builder.Services.AddScoped<ISupportCaseManagementService, SupportCaseManagementService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<ISupportCaseTokenService, SupportCaseTokenService>();
builder.Services.AddScoped<IPasswordHasher<ManagerProfile>, PasswordHasher<ManagerProfile>>();

// Register repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISkinProfileRepository, SkinProfileRepository>();
builder.Services.AddScoped<IConversationMessageRepository, ConversationMessageRepository>();
builder.Services.AddScoped<IIngredientCompatibilityRepository, IngredientCompatibilityRepository>();
builder.Services.AddScoped<IVectorSearchRepository, VectorSearchRepository>();
builder.Services.AddScoped<IGiftRepository, GiftRepository>();
builder.Services.AddScoped<IProductGiftMappingRepository, ProductGiftMappingRepository>();

// Register session manager
builder.Services.AddScoped<ISessionManager, SessionManager>();

// Register Phase 1 services (Quick Reply Handler)
builder.Services.AddScoped<IProductMappingService, ProductMappingService>();
builder.Services.AddScoped<IGiftSelectionService, GiftSelectionService>();
builder.Services.AddScoped<IFreeshipCalculator, FreeshipCalculator>();
builder.Services.AddScoped<IQuickReplyHandler, QuickReplyHandler>();

// Register state machine
builder.Services.AddScoped<IStateMachine, ConversationStateMachine>();

// Register state handlers
builder.Services.AddScoped<IStateHandler, IdleStateHandler>();
builder.Services.AddScoped<IStateHandler, QuickReplySalesStateHandler>();
builder.Services.AddScoped<IStateHandler, ConsultingStateHandler>();
builder.Services.AddScoped<IStateHandler, CollectingInfoStateHandler>();
builder.Services.AddScoped<IStateHandler, DraftOrderStateHandler>();
builder.Services.AddScoped<IStateHandler, CompleteStateHandler>();
builder.Services.AddScoped<IStateHandler, HumanHandoffStateHandler>();
builder.Services.AddScoped<IStateHandler, GreetingStateHandler>();
builder.Services.AddScoped<IStateHandler, MainMenuStateHandler>();
builder.Services.AddScoped<IStateHandler, BrowsingProductsStateHandler>();
builder.Services.AddScoped<IStateHandler, ProductDetailStateHandler>();
builder.Services.AddScoped<IStateHandler, VariantSelectionStateHandler>();
builder.Services.AddScoped<IStateHandler, AddToCartStateHandler>();
builder.Services.AddScoped<IStateHandler, CartReviewStateHandler>();
builder.Services.AddScoped<IStateHandler, ShippingAddressStateHandler>();
builder.Services.AddScoped<IStateHandler, PaymentMethodStateHandler>();
builder.Services.AddScoped<IStateHandler, OrderConfirmationStateHandler>();
builder.Services.AddScoped<IStateHandler, OrderPlacedStateHandler>();
builder.Services.AddScoped<IStateHandler, OrderTrackingStateHandler>();
builder.Services.AddScoped<IStateHandler, SkinAnalysisStateHandler>();
builder.Services.AddScoped<IStateHandler, SkinConsultationStateHandler>();
builder.Services.AddScoped<IStateHandler, HelpStateHandler>();
builder.Services.AddScoped<IStateHandler, ErrorStateHandler>();

// Register AI strategies
builder.Services.AddSingleton<IModelSelectionStrategy, HybridModelSelectionStrategy>();

// Register Gemini handlers
builder.Services.AddTransient<GeminiAuthHandler>();
builder.Services.AddTransient<GeminiRetryHandler>();

// Configure HttpClient for VertexAI Embedding Service
// Register concrete type first for cache wrapper to resolve
builder.Services.AddHttpClient<VertexAIEmbeddingService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<VertexAIOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

// Register interface mapping (will be overridden by cache wrapper if Redis enabled)
builder.Services.AddScoped<IEmbeddingService, VertexAIEmbeddingService>();

// Register Pinecone client as singleton (thread-safe, reusable)
builder.Services.AddSingleton<PineconeClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PineconeOptions>>().Value;
    return new PineconeClient(options.ApiKey);
});

// Register vector search services
builder.Services.AddSingleton<IIndexingProgressTracker, IndexingProgressTracker>();
builder.Services.AddScoped<IVectorSearchService, PineconeVectorService>();
builder.Services.AddScoped<ProductEmbeddingPipeline>();

// Register hybrid search services (Phase 3: RRF fusion)
builder.Services.AddScoped<KeywordSearchService>();
builder.Services.AddScoped<RRFFusionService>();
// Register concrete type first for cache wrapper to resolve
builder.Services.AddScoped<HybridSearchService>();
// Register interface mapping (will be overridden by cache wrapper if Redis enabled)
builder.Services.AddScoped<IHybridSearchService, HybridSearchService>();

// Register RAG services (Phase 5: Integration)
builder.Services.Configure<RAGOptions>(
    builder.Configuration.GetSection(RAGOptions.SectionName));
builder.Services.AddScoped<IContextAssembler, ContextAssembler>();
builder.Services.AddScoped<IRAGService, RAGService>();

// Add Redis distributed cache (Phase 4: Caching layer)
var redisEnabled = builder.Configuration.GetValue<bool>("Redis:Enabled", false);
if (redisEnabled)
{
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = builder.Configuration["Redis:InstanceName"];
        });

        // Register cache services
        builder.Services.AddSingleton<CacheKeyGenerator>();
        builder.Services.AddScoped<CacheInvalidationService>();

        // Wrap services with caching (manual decoration)
        builder.Services.AddScoped<IEmbeddingService>(sp =>
        {
            var innerService = sp.GetRequiredService<VertexAIEmbeddingService>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            var keyGenerator = sp.GetRequiredService<CacheKeyGenerator>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<EmbeddingCacheService>>();
            return new EmbeddingCacheService(innerService, cache, keyGenerator, configuration, logger);
        });

        builder.Services.AddScoped<IHybridSearchService>(sp =>
        {
            var innerService = sp.GetRequiredService<HybridSearchService>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            var keyGenerator = sp.GetRequiredService<CacheKeyGenerator>();
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<ResultCacheService>>();
            return new ResultCacheService(innerService, cache, keyGenerator, tenantContext, configuration, logger);
        });
    }
}

// Configure HttpClient for GeminiService with handlers
builder.Services.AddHttpClient<IGeminiService, GeminiService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddHttpMessageHandler<GeminiAuthHandler>()
    .AddHttpMessageHandler<GeminiRetryHandler>()
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Configure HttpClient for MessengerService with Polly resilience
builder.Services.AddHttpClient<IMessengerService, MessengerService>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    });

builder.Services.AddHttpClient<INobitaClient, NobitaClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<NobitaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    }
});

// Register background services
builder.Services.AddHostedService<WebhookProcessingService>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddHostedService<MessageCleanupService>();
builder.Services.AddHostedService<BotLockCleanupService>();

// Configure Channel for async event processing
var channel = Channel.CreateBounded<MessagingEvent>(
    new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest // Drop oldest to prevent blocking
    });
builder.Services.AddSingleton(channel);

var app = builder.Build();

// Auto-apply migrations on startup (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to apply database migrations");
        throw;
    }
}

// Validate critical configuration on startup
var facebookOpts = app.Services.GetRequiredService<IOptions<FacebookOptions>>().Value;
var webhookOpts = app.Services.GetRequiredService<IOptions<WebhookOptions>>().Value;
using var validationScope = app.Services.CreateScope();
var validationDbContext = validationScope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
var hasPageAccessTokenOverride = await validationDbContext.FacebookPageConfigs
    .IgnoreQueryFilters()
    .AnyAsync(x => x.IsActive && !string.IsNullOrWhiteSpace(x.PageAccessToken));

// if (string.IsNullOrWhiteSpace(facebookOpts.AppSecret))
//     throw new InvalidOperationException("Facebook:AppSecret is required. Configure via User Secrets or environment variables.");
// if (string.IsNullOrWhiteSpace(facebookOpts.PageAccessToken) && !hasPageAccessTokenOverride)
//     throw new InvalidOperationException("Facebook:PageAccessToken is required. Configure via User Secrets or environment variables.");
// if (string.IsNullOrWhiteSpace(webhookOpts.VerifyToken))
//     throw new InvalidOperationException("Webhook:VerifyToken is required. Configure via User Secrets or environment variables.");

var geminiOpts = app.Services.GetRequiredService<IOptions<GeminiOptions>>().Value;
if (string.IsNullOrWhiteSpace(geminiOpts.ApiKey))
    throw new InvalidOperationException("Gemini:ApiKey is required. Configure via User Secrets or environment variables.");

// Add signature validation middleware
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<SignatureValidationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<AdminTenantContextMiddleware>();
app.UseAntiforgery();

// Health check endpoint with detailed response
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// Webhook verification endpoint (GET)
app.MapGet("/webhook", (
    [FromQuery(Name = "hub.mode")] string? mode,
    [FromQuery(Name = "hub.verify_token")] string? verifyToken,
    [FromQuery(Name = "hub.challenge")] string? challenge,
    IOptions<WebhookOptions> options,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(verifyToken) || string.IsNullOrEmpty(challenge))
    {
        logger.LogWarning("Webhook verification failed: Missing parameters");
        return Results.BadRequest("Missing required parameters");
    }

    if (mode != "subscribe")
    {
        logger.LogWarning("Webhook verification failed: Invalid mode {Mode}", mode);
        return Results.StatusCode(403);
    }

    if (verifyToken != options.Value.VerifyToken)
    {
        logger.LogWarning("Webhook verification failed: Invalid verify token");
        return Results.StatusCode(403);
    }

    logger.LogInformation("Webhook verified successfully");
    return Results.Text(challenge);
})
.WithName("VerifyWebhook");

// Webhook event endpoint (POST)
app.MapPost("/webhook", async (
    WebhookEvent webhookEvent,
    Channel<MessagingEvent> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<Program> logger) =>
{
    if (webhookEvent.Object != "page")
    {
        logger.LogWarning("Invalid object type: {Object}", webhookEvent.Object);
        return Results.NotFound();
    }

    var eventCount = 0;
    foreach (var entry in webhookEvent.Entry)
    {
        // Handle messaging events (messages, postbacks)
        if (entry.Messaging != null)
        {
            foreach (var messagingEvent in entry.Messaging)
            {
                await channel.Writer.WriteAsync(messagingEvent);
                eventCount++;
            }
        }

        // Handle feed changes (live_comments)
        if (entry.Changes != null)
        {
            foreach (var change in entry.Changes)
            {
                if (change.Field == "live_comments" && change.Value?.CommentId != null)
                {
                    // Process live comment in background
                    _ = Task.Run(async () =>
                    {
                        using var scope = scopeFactory.CreateScope();
                        var liveCommentService = scope.ServiceProvider.GetRequiredService<ILiveCommentAutomationService>();
                        var commentLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                        try
                        {
                            var shouldHandle = await liveCommentService.ShouldHandleCommentAsync(
                                change.Value.Message ?? string.Empty);

                            if (shouldHandle)
                            {
                                await liveCommentService.ProcessCommentAsync(
                                    change.Value.CommentId,
                                    change.Value.From?.Id ?? string.Empty,
                                    change.Value.Message ?? string.Empty,
                                    change.Value.PostId ?? string.Empty);
                            }
                        }
                        catch (Exception ex)
                        {
                            commentLogger.LogError(ex, "Error processing live comment {CommentId}", change.Value.CommentId);
                        }
                    });

                    eventCount++;
                }
            }
        }
    }

    logger.LogInformation("Webhook received: {EventCount} events queued", eventCount);
    return Results.Ok(new { status = "EVENT_RECEIVED" });
})
.WithName("ReceiveWebhook");

// Metrics endpoint
app.MapGet("/metrics", (Channel<MessagingEvent> channel) =>
{
    var queueDepth = channel.Reader.Count;
    return Results.Ok(new
    {
        queue_depth = queueDepth,
        queue_capacity = 1000,
        queue_utilization_percent = (queueDepth * 100.0 / 1000),
        timestamp = DateTimeOffset.UtcNow
    });
});

// Root endpoint
app.MapGet("/", () => Results.Ok(new {
    status = "running",
    service = "MessengerWebhook"
}));

app.MapInternalOperationsEndpoints();
app.MapAdminAuthEndpoints();
app.MapAdminOperationsEndpoints();
app.MapTestRagEndpoints();
app.MapFallbackToFile("/admin/{*path:nonfile}", "admin/index.html");

using var adminBootstrapScope = app.Services.CreateScope();
var adminAuthService = adminBootstrapScope.ServiceProvider.GetRequiredService<IAdminAuthService>();
await adminAuthService.EnsureBootstrapManagerAsync();

// Validate Pinecone configuration at startup
var pineconeOptions = app.Services.GetRequiredService<IOptions<PineconeOptions>>().Value;
if (string.IsNullOrWhiteSpace(pineconeOptions.ApiKey))
{
    Log.Fatal("Pinecone:ApiKey is required. Set PINECONE_API_KEY in .env or User Secrets.");
    throw new InvalidOperationException("Pinecone:ApiKey is required. Set PINECONE_API_KEY in .env or User Secrets.");
}
if (string.IsNullOrWhiteSpace(pineconeOptions.IndexName))
{
    Log.Fatal("Pinecone:IndexName is required in appsettings.json");
    throw new InvalidOperationException("Pinecone:IndexName is required in appsettings.json");
}
Log.Information("Pinecone configuration validated: Index={IndexName}", pineconeOptions.IndexName);

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
