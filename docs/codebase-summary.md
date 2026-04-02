# Codebase Summary

**Project**: Multi-Tenant Messenger Chatbot Platform
**Last Updated**: 2026-04-02
**Phase**: Phase 3 Complete (Hybrid Search with RRF Fusion)

---

## Project Overview

Conversational commerce platform for cosmetics retail via Facebook Messenger. Built with ASP.NET Core 9.0, PostgreSQL with pgvector, and Google Gemini AI.

**Key Features**:
- AI-powered product recommendations via Google Gemini 2.0 Flash
- Hybrid search combining vector similarity + BM25 keyword search
- Reciprocal Rank Fusion (RRF) for optimal result ranking
- Skin analysis and personalized suggestions
- Multi-step conversation flows (17 states)
- Semantic product search via Pinecone vector database
- Session management with timeout handling
- Multi-tenant architecture with row-level security

---

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | ASP.NET Core | 9.0 |
| Language | C# | 12.0 |
| Database | PostgreSQL + pgvector | 16 |
| Vector DB | Pinecone | v2.0.0 |
| ORM | Entity Framework Core | 9.0 |
| AI | Google Gemini | 2.0 Flash |
| Embeddings | Vertex AI text-embedding-004 | 768-dim |
| Messaging | Facebook Messenger Platform | v21.0 |
| Testing | xUnit + Testcontainers | Latest |
| Containerization | Docker | Latest |

---

## Project Statistics

- **Total Files**: 112 files
- **Source Files**: 99 C# files
- **Test Files**: 42 test files
- **Total Tokens**: 254,071 tokens
- **Total Characters**: 798,874 chars
- **Lines of Code**: ~15,000+ LOC (estimated)

---

## Directory Structure

```
MessengerWebhook/
├── src/MessengerWebhook/
│   ├── Controllers/              # API endpoints (webhook)
│   ├── Data/
│   │   ├── Entities/            # Domain models (14 entities)
│   │   ├── Repositories/        # Data access layer (9 repos)
│   │   └── Migrations/          # EF Core migrations (4 migrations)
│   ├── Services/
│   │   ├── AI/                  # Gemini integration
│   │   │   ├── GeminiService.cs
│   │   │   ├── GeminiEmbeddingService.cs
│   │   │   ├── Handlers/        # Auth & retry handlers
│   │   │   ├── Models/          # AI request/response models
│   │   │   └── Strategies/      # Model selection strategies
│   │   ├── VectorSearch/        # Hybrid search (Phase 3)
│   │   │   ├── HybridSearchService.cs
│   │   │   ├── KeywordSearchService.cs
│   │   │   ├── RRFFusionService.cs
│   │   │   └── PineconeVectorService.cs
│   │   └── Messenger/           # Facebook Messenger API
│   ├── StateMachine/
│   │   ├── ConversationStateMachine.cs
│   │   ├── StateTransitionRules.cs
│   │   ├── Handlers/            # 12 state handlers
│   │   └── Models/              # State context & rules
│   ├── BackgroundServices/      # Cleanup services
│   ├── Middleware/              # Signature validation
│   ├── Configuration/           # Options classes
│   ├── HealthChecks/            # Health check endpoints
│   ├── Models/                  # Webhook DTOs
│   └── Prompts/                 # AI system prompts
├── tests/MessengerWebhook.Tests/
│   ├── Services/AI/             # AI service tests
│   ├── Data/Repositories/       # Repository tests
│   └── Fixtures/                # Test fixtures
├── docs/                        # Documentation
├── plans/                       # Implementation plans
└── .claude/                     # Claude Code configuration
```

---

## Core Components

### 1. Data Layer (`Data/`)

**Entities** (14 domain models):
- `Product`, `ProductVariant`, `ProductImage` - Product catalog
- `ConversationSession`, `ConversationMessage` - Session management
- `SkinProfile`, `IngredientCompatibility` - Personalization
- `Cart`, `CartItem`, `Order`, `OrderItem` - E-commerce
- `Color`, `Size` - Product attributes

**Repositories** (9 interfaces + implementations):
- `IProductRepository` - Product CRUD operations
- `ISessionRepository` - Session lifecycle management
- `ISkinProfileRepository` - User skin profile storage
- `IConversationMessageRepository` - Message history
- `IIngredientCompatibilityRepository` - Ingredient data
- `IVectorSearchRepository` - Semantic search via pgvector

**Migrations** (4 migrations):
1. `InitialCreate` - Base schema
2. `FixHighPriorityIssues` - Schema corrections
3. `UpdateSchemaForCosmetics` - Cosmetics domain entities
4. `AddProductEmbedding` - Vector embeddings for RAG

### 2. Services Layer (`Services/`)

#### AI Services (`Services/AI/`)

**GeminiService** (`GeminiService.cs`):
- Text generation via Gemini 2.0 Flash
- Chat completion with conversation history
- System prompt injection
- Retry logic with exponential backoff

**GeminiEmbeddingService** (`GeminiEmbeddingService.cs`):
- Text embedding generation (768 dimensions)
- Batch embedding support
- Used for semantic product search

**VectorSearchRepository** (`Data/Repositories/VectorSearchRepository.cs`):
- Semantic product search via cosine similarity
- pgvector integration
- Filters by category, price range, stock

**Handlers**:
- `GeminiAuthHandler` - API key authentication
- `GeminiRetryHandler` - Retry with exponential backoff

**Strategies**:
- `HybridModelSelectionStrategy` - Dynamic model selection

#### Messenger Services

**MessengerService** (`MessengerService.cs`):
- Send API integration
- Text messages, quick replies, templates
- Error handling and logging

**WebhookProcessor** (`WebhookProcessor.cs`):
- Incoming message routing
- State machine integration
- Handler resolution

**SignatureValidator** (`SignatureValidator.cs`):
- HMAC-SHA256 signature verification
- Webhook security

### 3. State Machine Layer (`StateMachine/`)

**ConversationStateMachine** (`ConversationStateMachine.cs`):
- Session lifecycle: load, create, save, reset
- Timeout handling (15min inactivity, 60min absolute)
- State transition validation
- Context serialization (JSON)

**StateTransitionRules** (`StateTransitionRules.cs`):
- 114 declarative transition rules
- Conditional transitions (e.g., cart must have items)
- Validation before state changes

**State Handlers** (`Handlers/`, 11 handlers + base):
1. `IdleStateHandler` - Initial state, awaits user message
2. `GreetingStateHandler` - Welcome message, transition to MainMenu
3. `MainMenuStateHandler` - Present main options (Browse, Skin Analysis, Help)
4. `BrowsingProductsStateHandler` - Product catalog with semantic search
5. `ProductDetailStateHandler` - Single product view with details
6. `SkinAnalysisStateHandler` - AI-powered skin analysis
7. `VariantSelectionStateHandler` - Choose product variant (color/size)
8. `AddToCartStateHandler` - Add item to cart
9. `CartReviewStateHandler` - Review cart contents
10. `ShippingAddressStateHandler` - Collect shipping info
11. `HelpStateHandler` - Context-aware help
12. `BaseStateHandler` - Abstract base with error handling

**State Context** (`Models/StateContext.cs`):
- Session data carrier
- Type-safe data storage (`GetData<T>`, `SetData`)
- Conversation history management
- Timeout detection

### 4. Background Services (`BackgroundServices/`)

**SessionCleanupService** (`SessionCleanupService.cs`):
- Runs every 10 minutes
- Deletes expired sessions
- Prevents database bloat

**MessageCleanupService** (`MessageCleanupService.cs`):
- Runs daily at 2 AM UTC
- Deletes messages older than 30 days
- Maintains message history retention

**WebhookProcessingService** (`WebhookProcessingService.cs`):
- Asynchronous message processing
- Channel-based queue
- Decouples webhook receipt from processing

### 5. Middleware (`Middleware/`)

**SignatureValidationMiddleware** (`SignatureValidationMiddleware.cs`):
- Validates `X-Hub-Signature-256` header
- HMAC-SHA256 verification
- Rejects unauthorized requests

### 6. Configuration (`Configuration/`)

**Options Classes**:
- `FacebookOptions` - App secret, page access token
- `GeminiOptions` - API key, model settings
- `WebhookOptions` - Verify token, queue settings

### 7. Health Checks (`HealthChecks/`)

**ChannelHealthCheck** (`ChannelHealthCheck.cs`):
- Monitors webhook processing queue
- Alerts on queue backlog

**GraphApiHealthCheck** (`GraphApiHealthCheck.cs`):
- Validates Facebook Graph API connectivity
- Checks page access token validity

---

## Database Schema

### Core Tables

**conversation_sessions**:
- Primary key: `id` (UUID)
- Unique: `facebook_psid` (TEXT)
- Fields: `current_state`, `context_json`, `last_activity_at`, `expires_at`
- Indexes: `facebook_psid`, `expires_at`

**products**:
- Primary key: `id` (UUID)
- Foreign key: `tenant_id` (for multi-tenancy)
- Fields: `name`, `description`, `price`, `category`, `brand`, `stock_quantity`
- Vector: `embedding` (vector(768)) for semantic search
- Indexes: `tenant_id`, `category`, `embedding` (HNSW)

**product_variants**:
- Primary key: `id` (UUID)
- Foreign key: `product_id`
- Fields: `sku`, `color_id`, `size_id`, `price`, `stock_quantity`

**skin_profiles**:
- Primary key: `id` (UUID)
- Foreign key: `facebook_psid`
- Fields: `skin_type`, `concerns`, `allergies`, `preferences` (JSONB)

**conversation_messages**:
- Primary key: `id` (UUID)
- Foreign key: `session_id`
- Fields: `role` (user/assistant), `content`, `timestamp`

**carts**, **cart_items**, **orders**, **order_items**:
- E-commerce entities (to be implemented in Phase 6)

---

## API Endpoints

### Webhook Endpoints

**GET /webhook**:
- Webhook verification
- Validates `hub.verify_token`
- Returns `hub.challenge`

**POST /webhook**:
- Receives incoming messages
- Validates signature
- Queues for processing

### Health Check Endpoints

**GET /health**:
- Overall health status
- Database connectivity
- Queue status
- Graph API connectivity

---

## State Machine Architecture

### Conversation States (17 states)

```
Idle (0) → Greeting (1) → MainMenu (2)
                            ├→ BrowsingProducts (3) → ProductDetail (4)
                            ├→ SkinConsultation (21)
                            ├→ OrderTracking (20)
                            └→ Help (30)

ProductDetail (4) → SkinAnalysis (5)
                 → VariantSelection (6) → AddToCart (7)

AddToCart (7) → CartReview (8) → ShippingAddress (9) → PaymentMethod (10)
                                → OrderConfirmation (11) → OrderPlaced (12)

Any state → Error (99) → Idle (0)
Any state → Help (30) → (return to previous state)
```

### State Transition Rules

**Total Rules**: 114 transition rules

**Rule Types**:
- Simple transitions (e.g., Idle → Greeting)
- Conditional transitions (e.g., CartReview → Checkout requires items in cart)
- Universal transitions (any state → Help, any state → Error)

**Validation**:
- All transitions validated before execution
- Invalid transitions logged and rejected
- Prevents invalid state jumps

### Session Management

**Timeouts**:
- **Inactivity**: 15 minutes → reset to Idle
- **Absolute**: 60 minutes → session expires

**Context Storage**:
- Serialized as JSON in `context_json` column
- Type-safe retrieval via `GetData<T>()`
- Supports complex objects (lists, dictionaries)

**Cleanup**:
- Background service runs every 10 minutes
- Deletes sessions past `expires_at`

---

## AI Integration

### Gemini Service

**Models Used**:
- `gemini-2.0-flash-exp` - Fast text generation
- `text-embedding-004` - 768-dimensional embeddings

**Features**:
- Chat completion with history
- System prompt injection
- Retry logic (3 attempts, exponential backoff)
- Error handling and logging

**System Prompt**:
- Located in `Prompts/beauty-consultant-system-prompt.txt`
- Defines AI persona as beauty consultant
- Includes product knowledge and conversation guidelines

### RAG (Retrieval-Augmented Generation)

**Architecture**:
1. Product descriptions embedded via `GeminiEmbeddingService`
2. Embeddings stored in `products.embedding` (vector(768))
3. User query embedded at runtime
4. Semantic search via cosine similarity (pgvector)
5. Top results passed to Gemini for response generation

**Vector Search**:
```csharp
var results = await _vectorSearchRepo.SearchSimilarProductsAsync(
    queryEmbedding,
    limit: 5,
    similarityThreshold: 0.7
);
```

---

## Testing Strategy

### Unit Tests

**Coverage**:
- AI services (GeminiService, GeminiEmbeddingService)
- State machine (ConversationStateMachine, handlers)
- Repositories (mocked database)
- Validators (signature validation)

**Framework**: xUnit with Moq

### Integration Tests

**Testcontainers**:
- PostgreSQL container with pgvector
- Real database for repository tests
- Automatic cleanup after tests

**Coverage**:
- VectorSearchRepository (semantic search)
- Database migrations
- End-to-end state transitions

---

## Configuration

### Environment Variables

**Required**:
- `Facebook__AppSecret` - Facebook app secret
- `Facebook__PageAccessToken` - Page access token
- `Facebook__VerifyToken` - Webhook verify token
- `Gemini__ApiKey` - Google Gemini API key
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string

**Optional**:
- `Gemini__Model` - Override default model
- `Webhook__MaxQueueSize` - Queue size limit
- `Logging__LogLevel__Default` - Log level

### appsettings.json

```json
{
  "Facebook": {
    "AppSecret": "your-app-secret",
    "PageAccessToken": "your-page-token",
    "VerifyToken": "your-verify-token"
  },
  "Gemini": {
    "ApiKey": "your-gemini-key",
    "Model": "gemini-2.0-flash-exp"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=messenger_bot;..."
  }
}
```

---

## Deployment

### Docker Support

**Dockerfile**:
- Multi-stage build
- ASP.NET Core 9.0 runtime
- Exposes port 8080

**docker-compose.yml**:
- App service
- PostgreSQL with pgvector
- Health checks
- Volume mounts

### Database Migrations

```bash
# Apply migrations
dotnet ef database update --project src/MessengerWebhook

# Generate SQL script
dotnet ef migrations script --project src/MessengerWebhook
```

---

## Development Phases

### Completed Phases

**Phase 1: Database Setup** ✅
- PostgreSQL with pgvector
- 14 domain entities
- 4 migrations
- Repository pattern

**Phase 2: Gemini Integration** ✅
- GeminiService for text generation
- GeminiEmbeddingService for embeddings
- Retry logic and error handling

**Phase 2.5: RAG Layer** ✅
- Vector search repository
- Semantic product search
- Integration tests with Testcontainers

**Phase 3: State Machine** ✅ (Completed: 2026-03-22)
- 17 conversation states
- 11 state handlers (IdleStateHandler, GreetingStateHandler, MainMenuStateHandler, BrowsingProductsStateHandler, ProductDetailStateHandler, SkinAnalysisStateHandler, VariantSelectionStateHandler, AddToCartStateHandler, CartReviewStateHandler, ShippingAddressStateHandler, HelpStateHandler)
- 114 transition rules with validation
- Session management with timeouts (15min inactivity, 60min absolute)
- SessionManager with IMemoryCache for performance
- WebhookProcessor integration complete
- Language detection in system prompt
- Unit tests: 139/139 passing
- Code review score: 8.5/10

### Pending Phases

**Phase 4: Product Catalog** (Next)
- Product browsing UI
- Category filtering
- Search functionality
- Product detail views

**Phase 5: Conversation Flows**
- Complete all state handlers
- Skin analysis implementation
- Cart management
- Order placement

**Phase 6: Order Workflow**
- Checkout flow
- Payment integration
- Order tracking
- Notifications

**Phase 7: Testing & Optimization**
- Comprehensive test coverage
- Performance optimization
- Load testing
- Bug fixes

**Phase 8: Multi-Tenant Architecture**
- Tenant management
- Branch isolation
- Multi-category support
- Scaling optimizations

---

## Key Design Decisions

### Architecture Patterns

**Repository Pattern**:
- Abstracts data access
- Enables unit testing with mocks
- Supports future caching layer

**State Pattern**:
- Each state isolated in own handler
- Easy to add new states
- Testable in isolation

**Dependency Injection**:
- All services registered in `Program.cs`
- Constructor injection throughout
- Supports testing and modularity

### Multi-Tenancy Strategy

**Shared Schema with Row-Level Security**:
- All tables have `tenant_id` column
- EF Core global query filters
- PostgreSQL RLS policies
- Cost-effective for 10-1000 tenants

### Error Handling

**Graceful Degradation**:
- State handlers catch exceptions
- Automatic transition to Error state
- User-friendly error messages
- Detailed logging for debugging

---

## Security Considerations

**Webhook Security**:
- HMAC-SHA256 signature validation
- Middleware validates all incoming requests
- Rejects unauthorized calls

**Data Security**:
- Tenant isolation via RLS
- No cross-tenant data leakage
- Parameterized queries (EF Core)

**Secrets Management**:
- Environment variables for production
- appsettings.json for development only
- No secrets in source control

---

## Performance Optimizations

**Database**:
- Indexes on foreign keys and frequently queried columns
- Vector index (HNSW) for semantic search
- Connection pooling

**Caching** (planned):
- Session caching (Redis)
- Product catalog caching
- Embedding cache

**Async/Await**:
- All I/O operations async
- Non-blocking request handling
- Scalable to high concurrency

---

## Monitoring & Observability

**Logging**:
- Structured logging via ILogger
- State transitions logged
- Error tracking with context
- Log levels: Trace, Debug, Info, Warning, Error, Critical

**Health Checks**:
- Database connectivity
- Queue status
- Graph API connectivity
- Exposed at `/health` endpoint

**Metrics** (planned):
- Message processing latency
- State transition frequency
- Error rates by state
- Session timeout rates

---

## Known Limitations

1. **Single Instance**: Not yet optimized for horizontal scaling
2. **Session Caching**: SessionManager uses IMemoryCache (Phase 3), product caching pending
3. **Limited Error Recovery**: Some error scenarios not fully handled
4. **Incomplete Handlers**: 6 state handlers pending (PaymentMethod, OrderConfirmation, OrderPlaced, OrderTracking, SkinConsultation, Error)
5. **No Multi-Tenancy**: Tenant resolution not yet implemented
6. **High-Priority Issues** (from Phase 3 code review):
   - Double-save pattern in BaseStateHandler/ConversationStateMachine
   - Null reference risk in BrowsingProductsStateHandler
   - SessionManager edge case in SaveAsync

---

## Future Enhancements

1. **Caching Layer**: Redis for sessions and products
2. **Event Sourcing**: Track all state transitions for analytics
3. **A/B Testing**: Multiple conversation strategies
4. **Multi-language**: i18n support
5. **Voice Messages**: Audio processing
6. **Image Recognition**: Product image search
7. **Analytics Dashboard**: Conversation flow analysis
8. **Admin Panel**: Tenant management UI

---

## References

- **Documentation**: `docs/`
- **Implementation Plans**: `plans/`
- **Source Code**: `src/MessengerWebhook/`
- **Tests**: `tests/MessengerWebhook.Tests/`
- **Architecture Decisions**: `docs/architecture-decision-records.md`
- **System Architecture**: `docs/system-architecture.md`
- **Code Standards**: `docs/code-standards.md`
