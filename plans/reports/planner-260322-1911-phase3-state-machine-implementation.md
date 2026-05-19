---
title: "Phase 3: State Machine Implementation Plan"
description: "Detailed implementation plan for conversation state machine with cosmetics-specific states"
status: pending
priority: P1
effort: 5d
branch: master
tags: [phase3, state-machine, conversation-flow, cosmetics]
created: 2026-03-22
---

# Phase 3: State Machine Implementation Plan

## Executive Summary

**Current State:**
- 77 C# files, 128 tests passing (47 integration + 81 unit)
- Phase 1 (Database) ✓ - Cosmetics schema complete
- Phase 2 (Gemini AI) ✓ - AI integration working
- Phase 2.5 (RAG Layer) ✓ - Vector search operational
- ConversationMessage entity ✓ - Already implemented
- ConversationSession entity ✓ - Basic structure exists
- WebhookProcessor - Currently stateless, needs state machine integration

**Gap Analysis:**
- ConversationState enum exists but incomplete (missing cosmetics-specific states)
- No state transition logic
- No state handlers
- No session management with caching
- No background cleanup services
- WebhookProcessor not integrated with state machine

**Deliverables:**
1. Enhanced ConversationState enum (15 states for cosmetics flow)
2. State machine core (IStateMachine, ConversationStateMachine)
3. State handlers (12 handlers for different conversation stages)
4. Session manager with memory caching
5. Background cleanup services (2 services)
6. WebhookProcessor integration
7. Comprehensive unit tests (50+ new tests)

---

## Architecture Overview

### State Flow for Cosmetics Sales

```
IDLE → GREETING → MAIN_MENU
  ↓
BROWSING_PRODUCTS → PRODUCT_DETAIL → SKIN_ANALYSIS
  ↓
VARIANT_SELECTION → ADD_TO_CART → CART_REVIEW
  ↓
SHIPPING_ADDRESS → PAYMENT_METHOD → ORDER_CONFIRMATION
  ↓
ORDER_PLACED → ORDER_TRACKING

Special States:
- HELP (accessible from any state)
- SKIN_CONSULTATION (parallel flow)
- ERROR (fallback state)
```

### Key Design Decisions

1. **State Storage**: JSON in ConversationSession.ContextJson (already exists)
2. **Caching Strategy**: IMemoryCache with 15min sliding expiration
3. **Timeout Policy**: 15min inactivity, 60min absolute
4. **History Management**: Last 10 messages via ConversationMessageRepository
5. **Cleanup Schedule**: Every 10min for sessions, daily for messages

---

## Implementation Phases

### Phase 3.1: Core State Machine (2 days)

**Files to Create:**
```
src/MessengerWebhook/StateMachine/
├── Models/
│   ├── StateContext.cs                    # Context wrapper with helpers
│   └── StateTransitionRule.cs             # Transition validation
├── IStateMachine.cs                       # Core interface
├── ConversationStateMachine.cs            # Main implementation
└── StateTransitionRules.cs                # Allowed transitions
```

**Files to Modify:**
```
src/MessengerWebhook/Data/Entities/ConversationSession.cs
  - Update ConversationState enum (add 6 new states)
  - Add helper methods for timeout checks
```

**Tasks:**

**T1. Update ConversationState Enum**
```csharp
public enum ConversationState
{
    Idle = 0,
    Greeting = 1,
    MainMenu = 2,
    BrowsingProducts = 3,
    ProductDetail = 4,
    SkinAnalysis = 5,           // NEW: Cosmetics-specific
    VariantSelection = 6,
    AddToCart = 7,
    CartReview = 8,
    ShippingAddress = 9,
    PaymentMethod = 10,
    OrderConfirmation = 11,
    OrderPlaced = 12,
    OrderTracking = 20,
    SkinConsultation = 21,      // NEW: Parallel flow
    Help = 30,
    Error = 99
}
```

**T2. Create StateContext Model**
- Wraps ConversationSession with typed helpers
- GetData<T>(key), SetData(key, value)
- IsTimedOut(timeout) check
- Serialize/deserialize ContextJson

**T3. Define StateTransitionRules**
- Static list of allowed transitions
- Conditional transitions (e.g., cart must have items)
- Validation logic per transition

**T4. Implement IStateMachine Interface**
```csharp
public interface IStateMachine
{
    Task<StateContext> LoadOrCreateAsync(string psid);
    Task<bool> TransitionToAsync(StateContext ctx, ConversationState newState);
    Task SaveAsync(StateContext ctx);
    Task<string> ProcessMessageAsync(string psid, string message);
    Task ResetAsync(string psid);
}
```

**T5. Implement ConversationStateMachine**
- Load session from repository
- Check timeout and reset if expired
- Validate transitions via StateTransitionRules
- Delegate to state handlers
- Save session after processing
- Logging for all state changes

**Success Criteria:**
- ✅ State transitions validated correctly
- ✅ Invalid transitions rejected with logging
- ✅ Timeout logic works (15min/60min)
- ✅ Session persists across app restarts
- ✅ Unit tests: 15 tests for state machine core

---

### Phase 3.2: State Handlers (2 days)

**Files to Create:**
```
src/MessengerWebhook/StateMachine/Handlers/
├── IStateHandler.cs                       # Base interface
├── BaseStateHandler.cs                    # Shared logic
├── IdleStateHandler.cs
├── GreetingStateHandler.cs
├── MainMenuStateHandler.cs
├── BrowsingProductsStateHandler.cs
├── ProductDetailStateHandler.cs
├── SkinAnalysisStateHandler.cs            # Cosmetics-specific
├── VariantSelectionStateHandler.cs
├── AddToCartStateHandler.cs
├── CartReviewStateHandler.cs
├── ShippingAddressStateHandler.cs
├── OrderConfirmationStateHandler.cs
└── HelpStateHandler.cs
```

**Handler Responsibilities:**

**IStateHandler Interface:**
```csharp
public interface IStateHandler
{
    ConversationState HandledState { get; }
    Task<string> HandleAsync(StateContext ctx, string message);
}
```

**BaseStateHandler (Abstract):**
- Common dependencies (IGeminiService, ISessionRepository)
- Helper methods (AddToHistory, TransitionTo)
- Error handling wrapper

**Key Handlers:**

**1. GreetingStateHandler**
- Welcome message with user's name (if available)
- Detect intent: browsing, skin consultation, order tracking
- Transition to MainMenu or specific flow

**2. BrowsingProductsStateHandler**
- Parse product search query
- Call RAG layer for semantic search
- Present top 5 products with quick replies
- Transition to ProductDetail on selection

**3. SkinAnalysisStateHandler** (Cosmetics-specific)
- Ask skin type questions (oily, dry, combination, sensitive)
- Store in StateContext.Data["skinProfile"]
- Use Gemini to recommend products
- Transition to BrowsingProducts with filtered results

**4. VariantSelectionStateHandler**
- Show available variants (size, color, scent)
- Validate stock availability
- Store selectedVariantId in context
- Transition to AddToCart

**5. CartReviewStateHandler**
- Display cart items with prices
- Calculate total (subtotal + shipping)
- Options: continue shopping, checkout, remove items
- Transition to ShippingAddress or BrowsingProducts

**6. ShippingAddressStateHandler**
- Parse address from natural language
- Validate address format
- Store in context
- Transition to PaymentMethod

**Success Criteria:**
- ✅ All 12 handlers implemented
- ✅ Each handler has clear transition logic
- ✅ Gemini integration for natural language understanding
- ✅ RAG layer integration for product search
- ✅ Unit tests: 25 tests for handlers (2-3 per handler)

---

### Phase 3.3: Session Management (1 day)

**Files to Create:**
```
src/MessengerWebhook/Services/
├── ISessionManager.cs
└── SessionManager.cs

src/MessengerWebhook/BackgroundServices/
├── SessionCleanupService.cs
└── MessageCleanupService.cs
```

**T1. Implement SessionManager**
- Wrap ISessionRepository with IMemoryCache
- Cache key: `session:{psid}`
- Sliding expiration: 15min
- Cache invalidation on save
- Thread-safe operations

**T2. Create SessionCleanupService**
- BackgroundService running every 10min
- Delete sessions where ExpiresAt < UtcNow
- Log cleanup count
- Handle exceptions gracefully

**T3. Create MessageCleanupService**
- BackgroundService running daily at 2 AM
- Delete messages older than 30 days
- Batch delete (1000 at a time)
- Log cleanup count

**Success Criteria:**
- ✅ Session cache reduces DB queries by 80%
- ✅ Cleanup services run on schedule
- ✅ No memory leaks from cached sessions
- ✅ Unit tests: 10 tests for session management

---

### Phase 3.4: WebhookProcessor Integration (0.5 days)

**Files to Modify:**
```
src/MessengerWebhook/Services/WebhookProcessor.cs
src/MessengerWebhook/Program.cs
```

**T1. Update WebhookProcessor**
- Inject IStateMachine, IConversationMessageRepository
- Replace direct Gemini call with state machine
- Load conversation history (last 10 messages)
- Save user message before processing
- Save AI response after processing
- Handle state machine errors gracefully

**T2. Register Services in DI**
```csharp
// State machine
builder.Services.AddSingleton<IStateMachine, ConversationStateMachine>();
builder.Services.AddScoped<ISessionManager, SessionManager>();

// State handlers (12 handlers)
builder.Services.AddScoped<IStateHandler, IdleStateHandler>();
// ... register all handlers

// Background services
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddHostedService<MessageCleanupService>();
```

**Success Criteria:**
- ✅ WebhookProcessor uses state machine
- ✅ Conversation history persisted
- ✅ All services registered in DI
- ✅ Integration tests: 5 tests for full flow

---

### Phase 3.5: Testing & Documentation (0.5 days)

**Test Coverage:**

**Unit Tests (50 new tests):**
- StateMachine: 15 tests
  - Valid/invalid transitions
  - Timeout logic
  - Session load/save
  - Reset functionality
- State Handlers: 25 tests
  - Each handler: happy path + error cases
  - Intent detection
  - Transition logic
- Session Manager: 10 tests
  - Cache hit/miss
  - Cleanup services
  - Concurrent access

**Integration Tests (5 new tests):**
- Full conversation flow (greeting → product → cart → order)
- State persistence across requests
- Timeout and recovery
- Cleanup service execution
- Error handling and fallback

**Documentation Updates:**
- Update `docs/system-architecture.md` with state machine diagram
- Update `docs/code-standards.md` with handler patterns
- Update `plans/260320-1042-gemini-sales-chatbot/phase-03-state-machine.md` status

**Success Criteria:**
- ✅ All 55 new tests pass
- ✅ Total test count: 183 tests (128 existing + 55 new)
- ✅ Code coverage: >80% for state machine code
- ✅ Documentation updated

---

## File Structure Summary

```
src/MessengerWebhook/
├── StateMachine/
│   ├── Models/
│   │   ├── StateContext.cs                    [NEW]
│   │   └── StateTransitionRule.cs             [NEW]
│   ├── Handlers/
│   │   ├── IStateHandler.cs                   [NEW]
│   │   ├── BaseStateHandler.cs                [NEW]
│   │   ├── IdleStateHandler.cs                [NEW]
│   │   ├── GreetingStateHandler.cs            [NEW]
│   │   ├── MainMenuStateHandler.cs            [NEW]
│   │   ├── BrowsingProductsStateHandler.cs    [NEW]
│   │   ├── ProductDetailStateHandler.cs       [NEW]
│   │   ├── SkinAnalysisStateHandler.cs        [NEW]
│   │   ├── VariantSelectionStateHandler.cs    [NEW]
│   │   ├── AddToCartStateHandler.cs           [NEW]
│   │   ├── CartReviewStateHandler.cs          [NEW]
│   │   ├── ShippingAddressStateHandler.cs     [NEW]
│   │   ├── OrderConfirmationStateHandler.cs   [NEW]
│   │   └── HelpStateHandler.cs                [NEW]
│   ├── IStateMachine.cs                       [NEW]
│   ├── ConversationStateMachine.cs            [NEW]
│   └── StateTransitionRules.cs                [NEW]
├── Services/
│   ├── ISessionManager.cs                     [NEW]
│   ├── SessionManager.cs                      [NEW]
│   └── WebhookProcessor.cs                    [MODIFY]
├── BackgroundServices/
│   ├── SessionCleanupService.cs               [NEW]
│   └── MessageCleanupService.cs               [NEW]
├── Data/Entities/
│   └── ConversationSession.cs                 [MODIFY]
└── Program.cs                                 [MODIFY]

tests/MessengerWebhook.UnitTests/
├── StateMachine/
│   ├── ConversationStateMachineTests.cs       [NEW]
│   ├── StateTransitionRulesTests.cs           [NEW]
│   └── Handlers/
│       ├── GreetingStateHandlerTests.cs       [NEW]
│       ├── BrowsingProductsHandlerTests.cs    [NEW]
│       └── ... (10 more handler tests)        [NEW]
└── Services/
    └── SessionManagerTests.cs                 [NEW]

tests/MessengerWebhook.IntegrationTests/
└── StateMachine/
    └── ConversationFlowTests.cs               [NEW]
```

**Total Files:**
- New: 30 files
- Modified: 3 files
- Tests: 55 new tests

---

## Dependencies & Integration Points

### Internal Dependencies
- **Phase 1 (Database)**: ConversationSession, ConversationMessage entities
- **Phase 2 (Gemini AI)**: IGeminiService for natural language processing
- **Phase 2.5 (RAG)**: IVectorSearchRepository for product search
- **Repositories**: ISessionRepository, IConversationMessageRepository

### External Dependencies
- **IMemoryCache**: Session caching (already in ASP.NET Core)
- **IHostedService**: Background cleanup services
- **ILogger**: Comprehensive logging for state changes

### Integration Flow
```
Webhook Event
  ↓
WebhookProcessor.ProcessMessageAsync()
  ↓
IStateMachine.ProcessMessageAsync(psid, message)
  ↓
1. Load session (cache → DB)
2. Load history (ConversationMessageRepository)
3. Get handler for current state
4. Handler.HandleAsync(context, message)
   ↓
   - Parse intent (Gemini)
   - Query products (RAG)
   - Update context
   - Transition state
5. Save session (DB → cache)
6. Save messages (ConversationMessageRepository)
  ↓
Send response to user
```

---

## Risk Assessment & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **Race conditions in concurrent sessions** | Medium | High | Use DB transactions, optimistic concurrency with RowVersion |
| **State corruption from invalid JSON** | Low | Medium | Validate JSON on load, fallback to Idle state |
| **Memory leak from cached sessions** | Low | Medium | Sliding expiration (15min), cleanup service |
| **Complex state logic hard to maintain** | Medium | Low | Clear handler separation, comprehensive tests, documentation |
| **Handler dependencies create circular refs** | Low | Medium | Use IServiceProvider for lazy resolution in handlers |
| **Timeout logic too aggressive** | Low | Low | Make timeouts configurable via appsettings.json |
| **Cleanup services impact performance** | Low | Low | Run during off-peak hours, batch operations |

---

## Testing Strategy

### Unit Test Approach
1. **State Machine Core**: Mock repositories, test transition logic in isolation
2. **State Handlers**: Mock dependencies (Gemini, RAG), test intent detection
3. **Session Manager**: Mock cache and repository, test cache behavior
4. **Cleanup Services**: Use in-memory database, test deletion logic

### Integration Test Approach
1. **Full Flow**: Use Testcontainers for PostgreSQL
2. **Real Dependencies**: Actual repositories, real cache
3. **Mock External APIs**: Mock Gemini and Facebook APIs
4. **Scenarios**:
   - Happy path: greeting → browse → add to cart → checkout
   - Timeout recovery: session expires, user returns
   - Error handling: invalid state, corrupted data
   - Cleanup: verify background services work

### Test Data Strategy
- **Fixtures**: Predefined conversation scenarios
- **Builders**: StateContext builder, ConversationSession builder
- **Mocks**: MockGeminiService, MockVectorSearchRepository

---

## Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Session load time | <20ms | 95th percentile |
| State transition time | <50ms | Average |
| Cache hit rate | >80% | After warmup |
| Cleanup service duration | <5s | Per run |
| Memory usage per session | <10KB | Cached context |
| Concurrent sessions | 1000+ | Load test |

---

## Security Considerations

### Data Protection
- **No PII in state context**: Store only IDs, not full addresses/names
- **Sensitive data in separate tables**: ShippingAddress, PaymentMethod
- **Audit trail**: Log all state transitions with timestamps

### Input Validation
- **Sanitize user input**: Before storing in context
- **Validate state transitions**: Prevent manipulation
- **Rate limiting**: Per user (future phase)

### Compliance
- **GDPR**: 30-day message retention, export/delete APIs (future)
- **PCI DSS**: Never store payment details in state context

---

## Configuration

**appsettings.json additions:**
```json
{
  "StateMachine": {
    "InactivityTimeoutMinutes": 15,
    "AbsoluteTimeoutMinutes": 60,
    "MessageRetentionDays": 30,
    "SessionCleanupIntervalMinutes": 10,
    "MessageCleanupSchedule": "0 2 * * *"
  }
}
```

---

## Rollout Plan

### Phase 3.1 (Day 1-2): Core State Machine
- Implement state machine core
- Write unit tests
- Code review

### Phase 3.2 (Day 3-4): State Handlers
- Implement all 12 handlers
- Write handler tests
- Integration with Gemini/RAG

### Phase 3.3 (Day 5): Session Management
- Implement session manager
- Create cleanup services
- Write tests

### Phase 3.4 (Day 5): Integration
- Update WebhookProcessor
- Register services in DI
- Integration tests

### Phase 3.5 (Day 5): Testing & Docs
- Run full test suite
- Update documentation
- Code review

---

## Success Metrics

**Functional:**
- ✅ All 17 conversation states implemented
- ✅ State transitions validated correctly
- ✅ Session persistence works across restarts
- ✅ Timeout logic functions (15min/60min)
- ✅ Cleanup services run on schedule

**Technical:**
- ✅ 55 new tests pass (50 unit + 5 integration)
- ✅ Total test count: 183 tests
- ✅ Code coverage: >80% for state machine
- ✅ No memory leaks detected
- ✅ Performance targets met (<20ms session load)

**Quality:**
- ✅ Code review passed
- ✅ No critical bugs in testing
- ✅ Documentation updated
- ✅ Logging comprehensive

---

## Next Steps After Phase 3

1. **Phase 4: Product Catalog**
   - Implement product browsing handlers
   - Build search and filter logic
   - Create product detail views

2. **Phase 5: Conversation Flows**
   - Implement cart management
   - Build checkout flow
   - Create order confirmation

3. **Phase 6: Order Workflow**
   - Implement order processing
   - Payment integration
   - Order tracking

---

## Unresolved Questions

1. **State Persistence Strategy**: Should we use optimistic concurrency (RowVersion) or pessimistic locking for session updates?
   - **Recommendation**: Start with optimistic concurrency, add locking only if race conditions occur

2. **Handler Registration**: Should handlers be registered as Scoped or Transient?
   - **Recommendation**: Scoped (same lifetime as request)

3. **Error Recovery**: What should happen if a handler throws an exception?
   - **Recommendation**: Transition to Error state, send apology message, log exception

4. **State Context Size Limit**: Should we limit ContextJson size to prevent DB bloat?
   - **Recommendation**: Yes, 10KB limit, log warning if exceeded

5. **Cleanup Service Timing**: Should MessageCleanupService run at fixed time or interval?
   - **Recommendation**: Fixed time (2 AM) to avoid peak hours

---

## Appendix: State Transition Matrix

| From State | To States | Conditions |
|------------|-----------|------------|
| Idle | Greeting | Any message |
| Greeting | MainMenu, SkinConsultation, OrderTracking | Intent detected |
| MainMenu | BrowsingProducts, SkinConsultation, OrderTracking, Help | User selection |
| BrowsingProducts | ProductDetail, CartReview, MainMenu | Product selected / view cart / back |
| ProductDetail | SkinAnalysis, VariantSelection, BrowsingProducts | Analyze skin / select variant / back |
| SkinAnalysis | BrowsingProducts | Analysis complete |
| VariantSelection | AddToCart, ProductDetail | Variant selected / back |
| AddToCart | CartReview, BrowsingProducts | Added to cart / continue shopping |
| CartReview | ShippingAddress, BrowsingProducts | Checkout / continue shopping |
| ShippingAddress | PaymentMethod, CartReview | Address entered / back |
| PaymentMethod | OrderConfirmation, ShippingAddress | Payment selected / back |
| OrderConfirmation | OrderPlaced | Confirmed |
| OrderPlaced | OrderTracking, MainMenu | Track order / new order |
| OrderTracking | MainMenu | Done tracking |
| Help | [Previous State] | Help provided |
| Error | Idle | Reset |

---

**Plan Created**: 2026-03-22 19:11 +07
**Estimated Effort**: 5 days
**Priority**: P1 (Critical)
**Status**: Pending approval
