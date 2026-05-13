# Codebase Summary

**Project**: Multi-Tenant Messenger Chatbot Platform
**Last Updated**: 2026-05-13
**Phase**: Phase 02 complete (Baseline Latency & Alerts) | Phase 01 complete (Observability & PII Protection) | R-05 complete (Program.cs modularization + SalesConsultationReplies extraction)

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

- **Total Files**: 430 packed files in `repomix-output.xml`
- **Output Tokens**: 837,952 tokens
- **Output Characters**: 3,182,720 chars
- **State handler source files**: 28 files under `src/MessengerWebhook/StateMachine/Handlers/`
- **Security exclusions during packing**: 2 credential-like files excluded by Repomix

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

#### Product Grounding Services (`Services/ProductGrounding/`)

- `ProductGroundingService`: Builds the allowed-product set from active selected products plus DB-confirmed RAG products, returns deterministic DB-backed related suggestions or a safe catalog fallback when exact product grounding is required but unavailable, and sanitizes assistant history that mentions unallowed products.
- `ProductNeedDetector`: Detects catalog/list, price, inventory, and product-fact turns that require grounding before Gemini can answer.
- `ProductMentionDetector`: Extracts product-like mentions from AI replies and history for grounding validation.
- `GroundedProduct`, `GroundedProductContext`: Typed product facts passed into prompt and response validation paths.

#### SubIntent Classification Services (`Services/SubIntent/`)

- `KeywordSubIntentDetector`: Rule-based detection for 70% of queries using keyword patterns (<50ms)
- `GeminiSubIntentClassifier`: AI-powered fallback for ambiguous queries (~1s)
- `HybridSubIntentClassifier`: Orchestrates keyword-first → AI fallback strategy
- Supports 6 SubIntent categories: ProductQuestion, PriceQuestion, ShippingQuestion, PolicyQuestion, AvailabilityQuestion, ComparisonQuestion
- Integrated into sales conversation flow via `SalesStateHandlerBase`
- RAG context can return detailed info (ingredients, skin types, benefits) when ProductQuestion detected
- System prompt has `{SUB_INTENT_CONTEXT}` placeholder for injecting category-specific guidance
- SubIntent logged with category, confidence, source for analytics
- Cost optimization: $0.075/month vs $3/month pure AI (97.5% reduction)
- Test coverage: 23/23 tests passing (20 unit + 3 integration)

#### Sales Services (`Services/Sales/`)

**SalesContextResolver** (`Services/Sales/Context/SalesContextResolver.cs`):
- Implements `ISalesContextResolver` interface
- Pure reads + in-memory `StateContext` mutations only — no DB writes, no Messenger sends
- Handles: VIP profile lookup, product resolution, history recovery, numbered suggestion detection, commercial fact snapshots, policy context sync
- Constructor deps: `ICustomerIntelligenceService`, `IProductMappingService`, `IGiftSelectionService`, `IFreeshipCalculator`, `IProductGroundingService`, `IGeminiService`, `ILogger`
- Methods: GetVipProfileAsync, GetActiveSelectedProductsAsync, ResolveCurrentProductAsync, ApplyResolvedProductAsync, TryExtractProductFromHistoryAsync, BuildCommercialFactSnapshotAsync, RefreshSelectedProductPolicyContextAsync, and more

**SalesPromptBuilder** (`Services/Sales/Prompt/SalesPromptBuilder.cs`):
- Implements `ISalesPromptBuilder` interface
- Completely pure: no async, no injected services, all methods return strings from input
- Handles: customer instruction building, CTA context, fact validation context, contact summaries, draft confirmation, state determination
- Methods: BuildCustomerInstruction, BuildCtaContext, BuildFactValidationContext, FormatAllowedProductNames, BuildPolicyGiftMessage, BuildPendingContactClarificationReply, BuildDraftConfirmation, GetContactSummary, DetermineNextState, and more

**SalesTextHelper** (`Utilities/SalesTextHelper.cs`):
- Internal static class for Vietnamese text normalization
- Method: `NormalizeForMatching(text)` - removes diacritics and normalizes spacing for product name matching
- Used by `SalesContextResolver` and remaining base-class predicates

**HistoryProductCandidate** (`Services/Sales/Context/HistoryProductCandidate.cs`):
- Public record for product candidates extracted from conversation history
- Properties: ProductName, ProductCode, Confidence, FoundInMessageIndex, Context

**ContactConfirmationFlow** (`Services/Sales/Contact/ContactConfirmationFlow.cs`):
- Implements `IContactConfirmationFlow` interface
- Encapsulates contact confirmation invariants: remembered contact reuse is always explicit, partial contacts are handled gracefully, generic buy continuations trigger reminder but not draft creation
- Pure orchestration: delegates to context resolver and prompt builder for decisions
- Methods: `EvaluateAsync(userMessage, salesContext, stateContext, cancellationToken)` returns decision object
- Handles: new contact detection, remembered contact reuse, partial contact (phone-only/address-only) scenarios, generic buy phrase detection, state-machine transitions for confirmation flow
- 66 unit tests covering all contact decision paths and edge cases

**SalesReplyOrchestrator** (`Services/Sales/Reply/SalesReplyOrchestrator.cs`, Phase R-04):
- Implements `ISalesReplyOrchestrator` interface (2 public methods: `GenerateAsync`, `BuildGroundedFallbackAsync`)
- Encapsulates 5-stage pipeline for AI-driven natural reply generation
- Methods: `BuildNaturalReplyAsync` (emotion→tone→smalltalk→Gemini→grounding), `GenerateDirectAIResponseAsync`, `BuildGroundedRelatedSuggestionOrFallbackAsync`, `RetrieveRagContextAsync`, `LogMetricsAsync`
- 558 LOC extracted from SalesStateHandlerBase (−430 line reduction in base class)
- Self-instantiation fallback in base class constructor (DI registered in Phase R-05)
- Handles: pipeline orchestration, RAG context retrieval, product grounding validation, response fallback, metrics logging
- Integrated with ConversationHistoryHelper (Phase R-05)

**SalesConsultationReplies** (`Services/Sales/Reply/SalesConsultationReplies.cs`, Phase R-05):
- Implements `ISalesConsultationReplies` interface
- Extracted 9 consultation reply builder methods from SalesStateHandlerBase (333 lines)
- Methods: BuildProductAskReply, BuildProductDetailReply, BuildNeedCheckReply, BuildSizeGuideReply, BuildIngredientReply, BuildConcernReply, BuildGiftWithProductReply, BuildGiftWithoutProductReply, BuildPolicyAskReply
- Constructor deps: `ISalesPromptBuilder`, `IProductGroundingService`, `ILogger`
- Pure orchestration: delegates text building to prompt builder, grounding validation to product service

**ConversationHistoryHelper** (`Services/Sales/ConversationHistoryHelper.cs`, Phase R-05):
- Static utility class to deduplicate history-related operations
- Methods: GetFormattedConversationHistory, AppendToConversationHistory
- Used by SalesStateHandlerBase, SalesReplyOrchestrator, CompleteStateHandler
- Eliminates code duplication in history building and formatting across services

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

**State Handlers**:
- Classic commerce handlers remain for catalog/cart flows (`IdleStateHandler`, `GreetingStateHandler`, `MainMenuStateHandler`, `BrowsingProductsStateHandler`, `ProductDetailStateHandler`, `VariantSelectionStateHandler`, `AddToCartStateHandler`, `CartReviewStateHandler`, `ShippingAddressStateHandler`, `PaymentMethodStateHandler`, `OrderConfirmationStateHandler`, `OrderPlacedStateHandler`, `OrderTrackingStateHandler`, `HelpStateHandler`, `ErrorStateHandler`).
- Sales-conversation handlers now also drive the Messenger order-closing flow (`ConsultingStateHandler`, `CollectingInfoStateHandler`, `DraftOrderStateHandler`, `CompleteStateHandler`, `QuickReplySalesStateHandler`, `HumanHandoffStateHandler`) on top of `SalesStateHandlerBase`.

**SalesStateHandlerBase** (840 lines after R-05 refactoring, reduced from 2425 lines):
- Fully refactored to delegate core tasks to extracted services via composition across 5 phases (R-01 through R-05)
- Owns: conversation state orchestration, order-context recovery, customer-contact memory, and state invariant enforcement only
- Delegates to `ISalesContextResolver`: product resolution, history recovery, policy context sync
- Delegates to `ISalesPromptBuilder`: prompt/response text building
- Delegates to `IContactConfirmationFlow`: contact confirmation decision logic
- Delegates to `ISalesReplyOrchestrator`: AI reply generation pipeline (5-stage: emotion→tone→smalltalk→Gemini→grounding)
- Delegates to `ISalesConsultationReplies`: consultation-specific reply building (Phase R-05)
- Delegates to `SalesMessageParser`: predicate logic for message classification (Phase R-05)
- Uses `ConversationHistoryHelper`: history operations (Phase R-05)
- DI registration for all services completed in Phase R-05 (`Program.cs`)
- Transcript production-readiness logic verified in code:
  - `DraftOrderStateHandler` first returns `TryCreateDraftConfirmationAsync(...)` output, then falls back to a generic local-draft acknowledgement.
  - `CompleteStateHandler` preserves completed-order context for greeting-prefixed order follow-ups, resets stale completed sessions after 24 hours, and answers `thông tin nào` with the concrete fields being rechecked plus any selected gift.

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
- Sales flow uses `Prompts/sales-closer-system-prompt.txt`
- Defines the sales closer persona and active-product guardrails for order, pricing, policy, and CTA replies
- Includes product knowledge via RAG context plus conversation guidelines
- Product-fact AI fallback excludes stale `selectedProductCodes` unless current tenant grounding exists

### RAG (Retrieval-Augmented Generation)

**Architecture**:
1. `RAGService` requires resolved `ITenantContext.TenantId`
2. Hybrid search receives a `tenant_id` filter for Pinecone/vector results
3. `KeywordSearchService` searches active products and filters by tenant when available
4. `ContextAssembler` re-loads only active products for the resolved tenant
5. Only DB-validated active tenant product IDs are passed to Gemini as grounding

**Safety behavior**:
- Catalog/list, price, inventory, and product-fact questions require active selected products or DB-validated RAG products.
- `SalesStateHandlerBase` builds a `GroundedProductContext` before Gemini calls in both natural and direct AI reply paths; if grounding is required but no allowed product exists, it returns deterministic DB-backed related suggestions when available, otherwise the safe catalog fallback.
- Assistant history is sanitized before prompt construction so stale ungrounded product names are not reused as evidence.
- Retrieved product names, codes, and prices are passed as structured grounding facts for prompt and validation use.

### SubIntent Classification

**Architecture**:
1. `HybridSubIntentClassifier` orchestrates keyword-first → AI fallback strategy
2. `KeywordSubIntentDetector` handles 70% of queries via pattern matching (<50ms)
3. `GeminiSubIntentClassifier` handles 30% ambiguous queries via AI (~1s)
4. Integrated into all 7 state handlers for context-aware routing

**Performance**:
- Latency: <500ms for 70% queries (keyword), ~1s for 30% (AI)
- Cost: $0.075/month vs $3/month pure AI (97.5% reduction)
- Accuracy: 95%+ on keyword patterns, 90%+ on AI fallback
- Test coverage: 23/23 tests passing (20 unit + 3 integration)

**Supported SubIntents** (13 types):
- Product: ProductQuestion, ProductList, ProductPrice, ProductInventory
- Policy: ShippingQuestion, GiftQuestion, PaymentQuestion
- Order: OrderConfirmation, OrderModification, OrderInquiry
- Support: Greeting, Thanks, HumanHandoff

---

## Testing Strategy

### Unit Tests

**Coverage**:
- AI services (GeminiService, GeminiEmbeddingService)
- State machine (ConversationStateMachine, handlers)
- Repositories (mocked database)
- Validators (signature validation)
- Sales services (SalesContextResolverTests: 43 tests, SalesPromptBuilderTests: 54 tests)

**Framework**: xUnit with Moq

**Test Metrics**:
- Total: 783 unit tests + 246 integration tests = 1029 tests
- R-02 additions: 97 new tests (54 SalesPromptBuilder + 43 SalesContextResolver)

### Integration Tests

**Testcontainers**:
- PostgreSQL container with pgvector
- Real database for repository tests
- Automatic cleanup after tests

**Coverage**:
- VectorSearchRepository (semantic search)
- Database migrations
- End-to-end state transitions
- R-02 services integration with state handlers

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

### Logging & Structured Events

**Logging Infrastructure**:
- Serilog structured logging with file sink
- All logs enriched with CorrelationId, TenantId, PsidHash (Phase 01)
- Log template: `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}`
- No raw PSID in logs (removed from 48 log calls across 21 files)
- OpenTelemetry distributed tracing (optional OTLP, Phase 01)

**Structured Log Events** (Phase 02):
- `SalesHandlerCompleted`: Response latency timing (milliseconds)
- `StateTransition`: State machine transitions (PascalCase)
- `WebhookError`: Error type classification and context
- All events carry enriched context (CorrelationId, TenantId, PsidHash)

**ILogger<T> Integration**:
- Dependency injection in all services (Phase 02 fixed SalesContextResolver and SalesReplyOrchestrator)
- Consistent logging patterns across state handlers
- Service-level logging in sales services (SalesPromptBuilder, ContactConfirmationFlow, SalesReplyOrchestrator)

### Alerting Infrastructure

**Telegram Notifier** (`Services/Alerts/TelegramNotifier.cs`):
- Alert delivery via Telegram Bot API
- Supports message formatting and context
- Integrated with alert deduplicator

**Alert Deduplicator** (`Services/Alerts/AlertDeduplicator.cs`):
- Prevents duplicate alert storms
- Configurable suppression windows
- Context-aware deduplication

**Alert Endpoints** (`Endpoints/AlertWebhookEndpointExtensions.cs`):
- Webhook endpoint mapping for alert delivery
- Integration with Telegram notifier
- Tenant isolation for multi-tenant alerting

**Request Timing Tracker** (`Services/Timing/RequestTimingTracker.cs`):
- Baseline latency measurement for responses
- Timing context in structured logs
- SalesHandlerCompleted events include response_latency_ms

**Seq Integration** (Phase 02):
- Structured log aggregation and querying
- Alert rules configured via Seq UI (not code)
- Baseline latency queries pending 7 days production data
- Custom dashboard ready for operational metrics

### Alert Runbooks

Created in `docs/runbooks/` directory (Phase 02):
- `alert-high-latency-response.md`: >2s response time diagnosis
- `alert-webhook-processing-failure.md`: Webhook retry and recovery
- `alert-ai-service-timeout.md`: Fallback guidance for Gemini timeouts

**Health Checks**:
- Database connectivity
- Queue status
- Graph API connectivity
- Exposed at `/health` endpoint

**Metrics** (A/B Testing + Metrics Platform, Phase 7):
- Conversation metrics via MetricsBackgroundService
- 8 metric types: ResponseTime, EmotionDetection, ToneMatching, ContextAnalysis, SmallTalkDetection, ValidationScore, PipelineOverhead, CacheHitRate
- Database-side aggregation via MetricsAggregationService
- Admin API endpoints for metrics reporting
- Real-time dashboard with CSV export

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
