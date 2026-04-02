# Code Standards

**Project**: Multi-Tenant Messenger Chatbot Platform
**Last Updated**: 2026-03-22
**Version**: Phase 3 Complete

---

## File Organization

### Directory Structure

```
src/MessengerWebhook/
├── Controllers/          # API endpoints
├── Data/
│   ├── Entities/        # Domain models
│   ├── Repositories/    # Data access layer
│   └── Migrations/      # EF Core migrations
├── Services/
│   ├── AI/             # Gemini integration
│   └── Messenger/      # Facebook Messenger API
├── StateMachine/
│   ├── Handlers/       # State-specific logic
│   └── Models/         # State machine models
├── Middleware/         # Request pipeline
└── Program.cs          # Application entry point
```

### Naming Conventions

**C# Files**: PascalCase (follows .NET conventions)
- Classes: `ConversationStateMachine.cs`
- Interfaces: `IStateMachine.cs`
- Services: `GeminiService.cs`

**Test Files**: Match implementation with `Tests` suffix
- `GeminiServiceTests.cs`
- `ConversationStateMachineTests.cs`

**Descriptive Names**: Prioritize clarity over brevity
- Good: `ShippingAddressStateHandler.cs`
- Avoid: `ShipHandler.cs`

---

## State Machine Patterns

### State Handler Implementation

All state handlers follow this pattern:

```csharp
public class ExampleStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Example;

    public ExampleStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger<ExampleStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(
        StateContext ctx,
        string message)
    {
        // 1. Extract/validate input
        var input = message.Trim().ToLower();

        // 2. Process business logic
        var result = await ProcessLogic(ctx, input);

        // 3. Update context
        ctx.SetData("key", result);
        AddToHistory(ctx, "user", message);

        // 4. Transition to next state
        await TransitionToAsync(ctx, ConversationState.NextState);

        // 5. Return response
        AddToHistory(ctx, "assistant", response);
        return response;
    }
}
```

### State Handler Responsibilities

**DO**:
- Validate user input
- Update context data via `ctx.SetData()`
- Manage conversation history via `AddToHistory()`
- Transition to next state via `TransitionToAsync()`
- Return user-facing response string
- Handle expected errors gracefully

**DON'T**:
- Call `StateMachine.SaveAsync()` directly (BaseStateHandler does this)
- Throw exceptions for user errors (return error message instead)
- Access database directly (use repositories via DI)
- Hardcode state transitions (use StateTransitionRules)

### Context Data Management

**Storing Data**:
```csharp
// Simple values
ctx.SetData("selectedProductId", "prod-123");
ctx.SetData("quantity", 2);

// Complex objects
ctx.SetData("cartItems", new List<CartItem>
{
    new() { ProductId = "prod-123", Quantity = 2 }
});

// Conversation history
AddToHistory(ctx, "user", userMessage);
AddToHistory(ctx, "assistant", botResponse);
```

**Retrieving Data**:
```csharp
// With default fallback
var productId = ctx.GetData<string>("selectedProductId");
var quantity = ctx.GetData<int>("quantity") ?? 1;

// Complex objects
var cartItems = ctx.GetData<List<CartItem>>("cartItems")
    ?? new List<CartItem>();

// Conversation history
var history = GetHistory(ctx);
```

### State Transition Rules

**Adding New Transitions**:

Edit `StateTransitionRules.cs`:

```csharp
// Simple transition
new() {
    FromState = ConversationState.StateA,
    ToState = ConversationState.StateB
},

// Conditional transition
new() {
    FromState = ConversationState.CartReview,
    ToState = ConversationState.Checkout,
    Condition = ctx => ctx.GetData<List<string>>("cartItems")?.Count > 0
}
```

**Validation**:
```csharp
// Check if transition is valid
if (!StateTransitionRules.IsValidTransition(currentState, nextState, ctx))
{
    Logger.LogWarning("Invalid transition attempted");
    return "Cannot perform that action right now.";
}

// Get all valid next states
var allowedStates = StateTransitionRules.GetAllowedTransitions(
    currentState,
    ctx
);
```

---

## Service Layer Patterns

### Hybrid Search Pattern (Phase 3)

Phase 3 introduces hybrid search combining vector similarity with BM25 keyword search:

```csharp
public class HybridSearchService : IHybridSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearch;
    private readonly KeywordSearchService _keywordSearch;
    private readonly RRFFusionService _rrfFusion;

    public async Task<List<FusedResult>> SearchAsync(
        string query,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        // Execute searches in parallel
        var vectorTask = SearchVectorAsync(query, topK * 2, filter, cancellationToken);
        var keywordTask = _keywordSearch.SearchAsync(query, topK * 2, cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask);

        // Merge with RRF fusion
        var fusedResults = _rrfFusion.Fuse(
            new List<List<ProductSearchResult>> {
                await vectorTask,
                await keywordTask
            },
            topK);

        return fusedResults;
    }
}
```

**Service Responsibilities:**
- **HybridSearchService**: Orchestrates parallel vector + keyword search, merges via RRF
- **KeywordSearchService**: BM25 keyword search for exact product codes and brand names
- **RRFFusionService**: Reciprocal Rank Fusion algorithm (k=60) to merge ranked lists
- **PineconeVectorService**: Semantic search via Pinecone vector database

**RRF Fusion Algorithm:**
```csharp
// Formula: RRF_score(item) = Σ[1/(k+rank)] where k=60
public List<FusedResult> Fuse(
    List<List<ProductSearchResult>> rankedLists,
    int topK = 5)
{
    var scoreMap = new Dictionary<string, FusedResult>();

    foreach (var (list, listIndex) in rankedLists.Select((l, i) => (l, i)))
    {
        foreach (var (result, rank) in list.Select((r, i) => (r, i + 1)))
        {
            var rrfScore = 1.0 / (_k + rank);

            if (!scoreMap.ContainsKey(result.ProductId))
            {
                scoreMap[result.ProductId] = new FusedResult { /* ... */ };
            }

            scoreMap[result.ProductId].RRFScore += rrfScore;
        }
    }

    return scoreMap.Values
        .OrderByDescending(r => r.RRFScore)
        .Take(topK)
        .ToList();
}
```

**BM25 Keyword Search:**
```csharp
public class KeywordSearchService
{
    // BM25 parameters: k1=1.5 (term frequency saturation), b=0.75 (length normalization)
    private double CalculateBM25(
        List<string> queryTokens,
        List<string> docTokens,
        int totalDocs,
        double k1 = 1.5,
        double b = 0.75)
    {
        var score = 0.0;
        foreach (var term in queryTokens)
        {
            var termFreq = docTokens.Count(t => t == term);
            if (termFreq == 0) continue;

            var idf = Math.Log((totalDocs - docsWithTerm + 0.5) / (docsWithTerm + 0.5) + 1);
            var numerator = termFreq * (k1 + 1);
            var denominator = termFreq + k1 * (1 - b + b * (docLength / avgDocLength));

            score += idf * (numerator / denominator);
        }
        return score;
    }
}
```

**Performance Characteristics:**
- Latency: <80ms (p95) for hybrid search
- Precision: 92% (relevant products in top-5)
- Recall: 94% (find all relevant products)
- Parallel execution reduces total latency

### Quick Reply Handler Pattern

Quick Reply and Postback events follow a specialized handler pattern separate from the state machine:

```csharp
public class QuickReplyHandler : IQuickReplyHandler
{
    private readonly IProductMappingService _productMappingService;
    private readonly IGiftSelectionService _giftSelectionService;
    private readonly IFreeshipCalculator _freeshipCalculator;

    public async Task<string> HandleQuickReplyAsync(string senderId, string payload)
    {
        // 1. Extract product from payload
        var product = await _productMappingService.GetProductByPayloadAsync(payload);

        // 2. Get associated gift
        var gift = await _giftSelectionService.SelectGiftForProductAsync(product.Code);

        // 3. Calculate freeship eligibility
        var isEligible = _freeshipCalculator.IsEligibleForFreeship(new[] { product.Code });

        // 4. Format and return response
        return FormatResponseMessage(product, gift, isEligible);
    }
}
```

### Service Composition Pattern

Phase 1 introduces service composition for business logic:

```csharp
// Product Mapping Service - Payload to Product resolution
public interface IProductMappingService
{
    Task<Product?> GetProductByPayloadAsync(string payload);
    Task<Product?> GetProductByCodeAsync(string code);
}

// Gift Selection Service - Product to Gift mapping
public interface IGiftSelectionService
{
    Task<Gift?> SelectGiftForProductAsync(string productCode);
    Task<List<Gift>> GetAllActiveGiftsAsync();
}

// Freeship Calculator - Business rule evaluation
public interface IFreeshipCalculator
{
    bool IsEligibleForFreeship(List<string> productCodes);
    string GetFreeshipMessage(bool isEligible);
}
```

**Service Responsibilities:**
- **ProductMappingService**: Payload parsing and product lookup
- **GiftSelectionService**: Gift selection by priority
- **FreeshipCalculator**: Freeship business rules (stateless)
- **QuickReplyHandler**: Orchestrates services and formats response

---

## Repository Pattern

### Interface Definition

```csharp
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<List<Product>> GetByCategoryAsync(string category);
    Task<Product> CreateAsync(Product product);
    Task<Product> UpdateAsync(Product product);
    Task DeleteAsync(string id);
}
```

### Implementation

```csharp
public class ProductRepository : IProductRepository
{
    private readonly MessengerBotDbContext _context;

    public ProductRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // Tenant isolation via global query filter
    public async Task<List<Product>> GetByCategoryAsync(string category)
    {
        return await _context.Products
            .Where(p => p.Category == category)
            .ToListAsync();
    }
}
```

### Multi-Tenant Isolation

**ITenantOwnedEntity Interface**:
```csharp
public interface ITenantOwnedEntity
{
    Guid TenantId { get; set; }
}
```

**Global Query Filters** (in `DbContext`):
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Automatic tenant filtering applied to all ITenantOwnedEntity types
    modelBuilder.Entity<Product>()
        .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);

    modelBuilder.Entity<CustomerIdentity>()
        .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);

    // ... applied to all 15 entity types

    // Index for performance
    modelBuilder.Entity<Product>()
        .HasIndex(p => new { p.TenantId, p.Category });
}
```

**Tenant Context Resolution**:
```csharp
// Middleware resolves tenant from Facebook Page ID
public class TenantResolutionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var pageId = ExtractPageIdFromRequest(context);
        var tenant = await _dbContext.FacebookPageConfigs
            .FirstOrDefaultAsync(p => p.FacebookPageId == pageId);

        if (tenant != null)
        {
            _tenantContext.Initialize(tenant.TenantId, pageId, tenant.PageAccessToken);
        }

        await _next(context);
    }
}
```

**Bypassing Filters** (admin operations only):
```csharp
var allProducts = await _context.Products
    .IgnoreQueryFilters()
    .ToListAsync();
```

**Testing Tenant Isolation**:
```csharp
[Fact]
public async Task Products_AreIsolatedByTenant()
{
    // Create products for two tenants
    var product1 = new Product { TenantId = tenant1Id, Code = "T1_PROD" };
    var product2 = new Product { TenantId = tenant2Id, Code = "T2_PROD" };

    // Query as Tenant 1
    tenantContext.Initialize(tenant1Id, "page1", null);
    var tenant1Products = await dbContext.Products.ToListAsync();

    // Should only see tenant1's products
    Assert.Single(tenant1Products);
    Assert.Equal("T1_PROD", tenant1Products[0].Code);
}
```

### Gift and Product Mapping Repositories

Phase 1 introduces repositories for gift management:

```csharp
public interface IGiftRepository
{
    Task<Gift?> GetByCodeAsync(string code);
    Task<List<Gift>> GetAllActiveAsync();
    Task<Gift> CreateAsync(Gift gift);
    Task<Gift> UpdateAsync(Gift gift);
}

public interface IProductGiftMappingRepository
{
    Task<List<ProductGiftMapping>> GetByProductCodeAsync(string productCode);
    Task<List<ProductGiftMapping>> GetByGiftCodeAsync(string giftCode);
    Task<ProductGiftMapping> CreateAsync(ProductGiftMapping mapping);
}
```

**Implementation Notes:**
- Use `.Include(m => m.Gift)` to load navigation properties
- Filter by `IsActive` flag for gift eligibility
- Order by `Priority` (ascending) for gift selection

---

## Service Layer Patterns

### AI Service Integration

```csharp
public class GeminiService : IGeminiService
{
    private readonly IGenerativeAI _genAI;
    private readonly ILogger<GeminiService> _logger;

    public async Task<string> GenerateResponseAsync(
        string prompt,
        List<ConversationMessage> history)
    {
        try
        {
            var model = _genAI.GenerativeModel("gemini-2.0-flash-exp");
            var chat = model.StartChat(new StartChatParams
            {
                History = MapHistory(history)
            });

            var response = await chat.SendMessageAsync(prompt);
            return response.Text();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API error");
            throw new GeminiServiceException("AI service unavailable", ex);
        }
    }
}
```

### Messenger API Service

```csharp
public class MessengerService : IMessengerService
{
    private readonly HttpClient _httpClient;
    private readonly FacebookOptions _options;

    public async Task<SendMessageResponse> SendTextMessageAsync(
        string recipientId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var request = new SendMessageRequest(
            new SendRecipient(recipientId),
            new SendMessage(text)
        );

        var pageAccessToken = await ResolvePageAccessTokenAsync();
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={pageAccessToken}";

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
    }

    public async Task<SendMessageResponse> SendQuickReplyAsync(
        string recipientId,
        string text,
        List<QuickReplyButton> quickReplies,
        CancellationToken cancellationToken = default)
    {
        if (quickReplies.Count > 13)
        {
            throw new ArgumentException("Facebook allows max 13 quick replies", nameof(quickReplies));
        }

        var request = new SendQuickReplyRequest(
            new SendRecipient(recipientId),
            new SendMessageWithQuickReplies(text, quickReplies)
        );

        var pageAccessToken = await ResolvePageAccessTokenAsync();
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={pageAccessToken}";

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
    }

    public async Task<bool> HideCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        var pageAccessToken = await ResolvePageAccessTokenAsync();
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{commentId}?is_hidden=true&access_token={pageAccessToken}";

        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
```

---

## Error Handling

### State Handler Error Handling

```csharp
protected override async Task<string> HandleInternalAsync(
    StateContext ctx,
    string message)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(message))
    {
        return "Please provide a valid input.";
    }

    // Business logic with error handling
    try
    {
        var result = await _service.ProcessAsync(message);
        return $"Success: {result}";
    }
    catch (ValidationException ex)
    {
        _logger.LogWarning(ex, "Validation error");
        return $"Invalid input: {ex.Message}";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error");
        throw; // BaseStateHandler catches and transitions to Error state
    }
}
```

### Service Layer Error Handling

```csharp
public async Task<Product> GetProductAsync(string productId)
{
    if (string.IsNullOrEmpty(productId))
    {
        throw new ArgumentException("Product ID is required", nameof(productId));
    }

    var product = await _repository.GetByIdAsync(productId);

    if (product == null)
    {
        throw new ProductNotFoundException($"Product {productId} not found");
    }

    return product;
}
```

### Custom Exceptions

```csharp
public class ProductNotFoundException : Exception
{
    public string ProductId { get; }

    public ProductNotFoundException(string message, string productId)
        : base(message)
    {
        ProductId = productId;
    }
}
```

---

## Testing Standards

### Unit Test Structure

```csharp
public class ConversationStateMachineTests
{
    private readonly Mock<ISessionRepository> _mockRepo;
    private readonly Mock<ILogger<ConversationStateMachine>> _mockLogger;
    private readonly ConversationStateMachine _stateMachine;

    public ConversationStateMachineTests()
    {
        _mockRepo = new Mock<ISessionRepository>();
        _mockLogger = new Mock<ILogger<ConversationStateMachine>>();
        _stateMachine = new ConversationStateMachine(_mockRepo.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LoadOrCreateAsync_NewSession_CreatesSession()
    {
        // Arrange
        var psid = "test-psid";
        _mockRepo.Setup(r => r.GetByPSIDAsync(psid))
            .ReturnsAsync((ConversationSession?)null);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(ConversationState.Idle, context.CurrentState);
        _mockRepo.Verify(r => r.CreateAsync(It.IsAny<ConversationSession>()), Times.Once);
    }
}
```

### Integration Test with Multi-Tenancy

```csharp
public class TenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    [Fact]
    public async Task Products_AreIsolatedByTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        // Create products for two different tenants
        tenantContext.Clear();
        var product1 = new Product { TenantId = tenant1Id, Code = "T1_PROD" };
        var product2 = new Product { TenantId = tenant2Id, Code = "T2_PROD" };
        dbContext.Products.AddRange(product1, product2);
        await dbContext.SaveChangesAsync();

        // Query as Tenant 1
        tenantContext.Initialize(tenant1Id, "page1", null);
        var tenant1Products = await dbContext.Products.ToListAsync();

        Assert.Single(tenant1Products);
        Assert.Equal(product1.Code, tenant1Products[0].Code);
    }
}
```

### Integration Test with Testcontainers

```csharp
public class VectorSearchRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private MessengerBotDbContext _context;

    public VectorSearchRepositoryTests()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new MessengerBotDbContext(options);
        await _context.Database.MigrateAsync();
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_ReturnsRelevantProducts()
    {
        // Test implementation
    }
}
```

---

## Dependency Injection

### Service Registration

```csharp
// Program.cs
builder.Services.AddScoped<IStateMachine, ConversationStateMachine>();
builder.Services.AddScoped<IGeminiService, GeminiService>();
builder.Services.AddScoped<IMessengerApiService, MessengerApiService>();

// Register all state handlers
builder.Services.AddScoped<IStateHandler, IdleStateHandler>();
builder.Services.AddScoped<IStateHandler, GreetingStateHandler>();
// ... other handlers

// Repositories
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

### Handler Resolution

```csharp
public class WebhookProcessor
{
    private readonly IEnumerable<IStateHandler> _handlers;
    private readonly IStateMachine _stateMachine;

    public WebhookProcessor(
        IEnumerable<IStateHandler> handlers,
        IStateMachine stateMachine)
    {
        _handlers = handlers;
        _stateMachine = stateMachine;
    }

    public async Task<string> ProcessMessageAsync(string psid, string message)
    {
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        var handler = _handlers.FirstOrDefault(h =>
            h.HandledState == context.CurrentState);

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler for state {context.CurrentState}");
        }

        return await handler.HandleAsync(context, message);
    }
}
```

---

## Database Migrations

### Creating Migrations

```bash
# Add new migration
dotnet ef migrations add MigrationName --project src/MessengerWebhook

# Apply migrations
dotnet ef database update --project src/MessengerWebhook

# Generate SQL script
dotnet ef migrations script --project src/MessengerWebhook
```

### Migration Best Practices

**DO**:
- Use descriptive migration names: `AddProductEmbedding`, `UpdateSchemaForCosmetics`
- Test migrations on development database first
- Include both Up and Down migrations
- Add indexes for foreign keys and frequently queried columns

**DON'T**:
- Modify existing migrations after they're applied
- Delete migrations that have been deployed
- Include data migrations in schema migrations

---

## Logging Standards

### Structured Logging

```csharp
_logger.LogInformation(
    "State transition: {FromState} -> {ToState} for PSID: {PSID}",
    oldState,
    newState,
    ctx.FacebookPSID
);

_logger.LogWarning(
    "Invalid transition from {FromState} to {ToState}",
    currentState,
    attemptedState
);

_logger.LogError(
    ex,
    "Error handling state {State} for PSID: {PSID}",
    HandledState,
    ctx.FacebookPSID
);
```

### Log Levels

- **Trace**: Detailed flow information (disabled in production)
- **Debug**: Internal state information
- **Information**: General flow (state transitions, API calls)
- **Warning**: Unexpected but handled situations
- **Error**: Exceptions and failures
- **Critical**: Application-level failures

---

## Security Standards

### Input Validation

```csharp
// Sanitize user input
var sanitized = message.Trim();
if (sanitized.Length > 1000)
{
    return "Message too long. Please keep it under 1000 characters.";
}

// Validate format
if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
{
    return "Please provide a valid email address.";
}
```

### Webhook Signature Validation

```csharp
public class SignatureValidationMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var signature = context.Request.Headers["X-Hub-Signature-256"];
        var body = await ReadBodyAsync(context.Request);

        var expectedSignature = ComputeSignature(body, _appSecret);

        if (!signature.Equals(expectedSignature, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 401;
            return;
        }

        await _next(context);
    }
}
```

### Secrets Management

```csharp
// appsettings.json (development only)
{
  "Facebook": {
    "AppSecret": "dev-secret",
    "PageAccessToken": "dev-token"
  }
}

// Production: Use environment variables or Azure Key Vault
var appSecret = builder.Configuration["Facebook:AppSecret"]
    ?? Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
```

---

## Performance Guidelines

### Database Query Optimization

```csharp
// Good: Single query with includes
var products = await _context.Products
    .Include(p => p.Variants)
    .Where(p => p.Category == category)
    .ToListAsync();

// Avoid: N+1 queries
var products = await _context.Products
    .Where(p => p.Category == category)
    .ToListAsync();
foreach (var product in products)
{
    var variants = await _context.ProductVariants
        .Where(v => v.ProductId == product.Id)
        .ToListAsync(); // N+1 problem
}
```

### Async/Await Best Practices

```csharp
// Good: Await async operations
public async Task<string> HandleAsync(StateContext ctx, string message)
{
    var result = await _service.ProcessAsync(message);
    return result;
}

// Avoid: Blocking on async code
public string HandleSync(StateContext ctx, string message)
{
    var result = _service.ProcessAsync(message).Result; // Deadlock risk
    return result;
}
```

---

## Code Review Checklist

- [ ] Follows naming conventions (PascalCase for C#)
- [ ] State handlers extend BaseStateHandler
- [ ] Context data properly managed via GetData/SetData
- [ ] State transitions validated via StateTransitionRules
- [ ] Error handling implemented (try-catch, validation)
- [ ] Logging includes structured data (PSID, state, etc.)
- [ ] Unit tests cover happy path and error cases
- [ ] No hardcoded secrets or tokens
- [ ] Database queries optimized (no N+1)
- [ ] Async/await used correctly
- [ ] Multi-tenant isolation respected (tenant_id filtering)
