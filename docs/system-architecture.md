# System Architecture

**Project**: Multi-Tenant Messenger Chatbot Platform
**Last Updated**: 2026-03-22
**Version**: Phase 3 Complete

---

## Overview

Multi-tenant conversational commerce platform for cosmetics retail via Facebook Messenger. Architecture follows clean separation: Data → Services → State Machine → Webhook API.

## Architecture Layers

### 1. Data Layer

**Location**: `src/MessengerWebhook/Data/`

**Components**:
- **Entities**: Domain models (Product, ConversationSession, SkinProfile, etc.)
- **DbContext**: EF Core context with pgvector extension
- **Repositories**: Data access abstraction
- **Migrations**: Schema versioning

**Key Features**:
- PostgreSQL with pgvector for semantic search
- Multi-tenant isolation via `tenant_id` column
- Row-level security policies
- Embedding storage for RAG

### 2. Services Layer

**Location**: `src/MessengerWebhook/Services/`

**AI Services** (`Services/AI/`):
- `GeminiService`: Text generation, chat completion
- `GeminiEmbeddingService`: Vector embeddings for RAG
- `VectorSearchRepository`: Semantic product search

**Messenger Services** (`Services/Messenger/`):
- `MessengerApiService`: Send API integration
- `WebhookProcessor`: Incoming message routing

### 3. State Machine Layer

**Location**: `src/MessengerWebhook/StateMachine/`

**Architecture**: Finite State Machine (FSM) with 17 conversation states.

#### Core Components

**ConversationStateMachine** (`ConversationStateMachine.cs`):
- Session lifecycle management
- State persistence to database
- Timeout handling (15min inactivity, 60min absolute)
- State transition validation

**StateTransitionRules** (`StateTransitionRules.cs`):
- Declarative transition rules (114 rules total)
- Conditional transitions based on context
- Validation before state changes

**StateContext** (`Models/StateContext.cs`):
```csharp
public class StateContext
{
    public string SessionId { get; set; }
    public string FacebookPSID { get; set; }
    public ConversationState CurrentState { get; set; }
    public Dictionary<string, object> Data { get; set; }
    public DateTime LastInteractionAt { get; set; }

    public T? GetData<T>(string key);
    public void SetData(string key, object value);
    public bool IsTimedOut(TimeSpan timeout);
}
```

#### State Handlers

**Base Handler** (`Handlers/BaseStateHandler.cs`):
- Abstract base for all state handlers
- Error handling with automatic Error state transition
- Conversation history management
- State persistence after handling

**Implemented Handlers** (12 handlers):
1. `IdleStateHandler` - Initial state, awaits user message
2. `GreetingStateHandler` - Welcome message, transition to MainMenu
3. `MainMenuStateHandler` - Present main options
4. `BrowsingProductsStateHandler` - Product catalog browsing
5. `ProductDetailStateHandler` - Single product details
6. `SkinAnalysisStateHandler` - AI-powered skin analysis
7. `VariantSelectionStateHandler` - Choose product variant
8. `AddToCartStateHandler` - Add item to cart
9. `CartReviewStateHandler` - Review cart contents
10. `ShippingAddressStateHandler` - Collect shipping info
11. `HelpStateHandler` - Context-aware help
12. `BaseStateHandler` - Abstract base class

#### State Diagram

```
Idle → Greeting → MainMenu
                    ├→ BrowsingProducts → ProductDetail → VariantSelection → AddToCart → CartReview
                    ├→ SkinConsultation → BrowsingProducts
                    ├→ OrderTracking
                    └→ Help (accessible from any state)

CartReview → ShippingAddress → PaymentMethod → OrderConfirmation → OrderPlaced → OrderTracking

Any state → Error → Idle
Any state → Help → (return to previous state)
```

#### Session Management

**Timeouts**:
- **Inactivity**: 15 minutes (resets to Idle)
- **Absolute**: 60 minutes (session expires)

**Persistence**:
- State saved to `conversation_sessions` table
- Context data serialized as JSON
- Automatic cleanup via background service

**Context Data Storage**:
```csharp
ctx.SetData("cartItems", new List<string> { "product-123" });
ctx.SetData("selectedProductId", "prod-456");
ctx.SetData("conversationHistory", messages);
```

### 4. API Layer

**Location**: `src/MessengerWebhook/Controllers/`

**WebhookController**:
- Webhook verification (GET)
- Message processing (POST)
- Signature validation middleware

**Flow**:
```
Facebook → WebhookController → WebhookProcessor → StateMachine → StateHandler → Response
```

### 5. Middleware

**SignatureValidationMiddleware**:
- Validates `X-Hub-Signature-256` header
- HMAC-SHA256 verification
- Prevents unauthorized webhook calls

## Data Flow

### Incoming Message Flow

```
1. Facebook sends POST to /webhook
2. SignatureValidationMiddleware validates request
3. WebhookController extracts message data
4. WebhookProcessor routes to StateMachine
5. StateMachine loads/creates session
6. StateMachine checks for timeout
7. StateMachine gets appropriate StateHandler
8. StateHandler processes message
9. StateHandler may call GeminiService for AI response
10. StateHandler may query VectorSearchRepository for products
11. StateHandler updates context and transitions state
12. StateMachine persists session
13. Response sent via MessengerApiService
```

### State Transition Flow

```
1. Handler calls StateMachine.TransitionToAsync(ctx, newState)
2. StateMachine validates transition via StateTransitionRules
3. If valid: update ctx.CurrentState, log transition
4. If invalid: log warning, return false
5. Handler saves context via StateMachine.SaveAsync(ctx)
6. Session persisted to database with new state
```

## Database Schema

### Core Tables

**conversation_sessions**:
- `id` (PK)
- `facebook_psid` (unique)
- `current_state` (enum)
- `context_json` (JSONB)
- `last_activity_at`
- `expires_at`
- `created_at`

**products**:
- `id` (PK)
- `tenant_id` (FK)
- `name`, `description`, `price`
- `embedding` (vector(768)) - for semantic search
- `category`, `brand`, `stock_quantity`

**skin_profiles**:
- `id` (PK)
- `facebook_psid` (FK)
- `skin_type`, `concerns`, `allergies`
- `preferences` (JSONB)

**conversation_messages**:
- `id` (PK)
- `session_id` (FK)
- `role` (user/assistant)
- `content`
- `timestamp`

## Multi-Tenancy

**Strategy**: Shared schema with row-level security (RLS)

**Implementation**:
- Every table has `tenant_id` column
- EF Core global query filters
- PostgreSQL RLS policies
- Tenant resolution via `facebook_page_id` lookup

**Isolation**:
```csharp
modelBuilder.Entity<Product>()
    .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);
```

## Security

**Webhook Security**:
- HMAC-SHA256 signature validation
- App secret verification
- HTTPS only

**Data Security**:
- Tenant isolation via RLS
- No cross-tenant data leakage
- Audit logging for sensitive operations

**API Security**:
- Rate limiting (planned)
- Input validation
- SQL injection prevention via EF Core

## Performance Considerations

**Caching**:
- Session caching (planned)
- Product catalog caching (planned)
- Embedding cache for frequent queries

**Database Optimization**:
- Indexes on `tenant_id`, `facebook_psid`, `facebook_page_id`
- Vector index (HNSW) on product embeddings
- Connection pooling

**Scalability**:
- Stateless API (sessions in DB)
- Horizontal scaling ready
- Background job processing for cleanup

## Monitoring & Observability

**Logging**:
- Structured logging via ILogger
- State transitions logged
- Error tracking with context

**Metrics** (planned):
- Message processing latency
- State transition frequency
- Error rates by state
- Session timeout rates

## Technology Stack

- **Framework**: ASP.NET Core 9.0
- **Database**: PostgreSQL 16 with pgvector
- **ORM**: Entity Framework Core 9.0
- **AI**: Google Gemini 2.0 Flash
- **Messaging**: Facebook Messenger Platform
- **Testing**: xUnit, Testcontainers

## Deployment Architecture

**Current**: Single instance development

**Production** (planned):
- Load balancer → Multiple API instances
- PostgreSQL primary + read replicas
- Redis for session caching
- Background worker for cleanup jobs
- Monitoring via Application Insights

## Future Enhancements

1. **Caching Layer**: Redis for sessions and products
2. **Event Sourcing**: Track all state transitions
3. **Analytics**: Conversation flow analysis
4. **A/B Testing**: Multiple conversation strategies
5. **Multi-language**: i18n support
6. **Voice Messages**: Audio processing
7. **Image Recognition**: Product image search
