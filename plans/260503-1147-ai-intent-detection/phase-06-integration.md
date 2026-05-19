# Phase 6: Integration with State Handlers

**Priority:** P1  
**Status:** pending  
**Effort:** 2h  
**Dependencies:** Phase 5 (Configuration & DI)

## Context Links

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md` (lines 217-270)
- State handler base: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Consulting handler: `src/MessengerWebhook/StateMachine/Handlers/ConsultingStateHandler.cs`
- Topic analyzer (to replace): `src/MessengerWebhook/Services/Conversation/TopicAnalyzer.cs`

## Overview

Integrate `ISubIntentClassifier` into state handlers, replacing `TopicAnalyzer` keyword-only approach. Use sub-intent classification to route customer questions to appropriate response strategies.

## Key Insights

- `SalesStateHandlerBase` already injects many services - add `ISubIntentClassifier`
- `ConsultingStateHandler` likely uses topic analysis for question routing
- Need conversation context from state machine (current state, product selection)
- Keep `TopicAnalyzer` as fallback (don't delete, mark deprecated)
- Sub-intent drives response strategy: product questions → RAG, price → pricing service, etc.

## Requirements

### Functional
- Inject `ISubIntentClassifier` into `SalesStateHandlerBase`
- Build `ConversationContext` from state machine state
- Use sub-intent to route questions in `ConsultingStateHandler`
- Log sub-intent classification results for debugging
- Fallback to `TopicAnalyzer` if classifier returns null
- Handle all 6 sub-intent categories with appropriate responses

### Non-Functional
- No breaking changes to existing handlers
- Backwards compatible (TopicAnalyzer still works)
- Performance: <1s p95 latency for question handling
- Observability: log sub-intent, confidence, source

## Architecture

```
SalesStateHandlerBase
├── ISubIntentClassifier (injected)
└── BuildConversationContext(state) → ConversationContext

ConsultingStateHandler
├── HandleAsync(message, state)
│   ├── Classify sub-intent
│   ├── Route by category:
│   │   ├── ProductQuestion → RAG service
│   │   ├── PriceQuestion → Pricing service
│   │   ├── ShippingQuestion → Shipping info
│   │   ├── PolicyQuestion → Policy guard
│   │   ├── AvailabilityQuestion → Product repo
│   │   └── ComparisonQuestion → Product comparison
│   └── Generate response
└── Fallback to TopicAnalyzer if null
```

**Data Flow:**
```
User Message
    ↓
ConsultingStateHandler.HandleAsync()
    ↓
BuildConversationContext(state)
    ↓
ISubIntentClassifier.ClassifyAsync(message, context)
    ↓
Route by SubIntentCategory
    ├─ ProductQuestion → RAGService.QueryAsync()
    ├─ PriceQuestion → GetPricingInfo()
    ├─ ShippingQuestion → GetShippingInfo()
    └─ ...
    ↓
Generate response with Gemini
```

## Related Code Files

**To modify:**
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (inject classifier)
- `src/MessengerWebhook/StateMachine/Handlers/ConsultingStateHandler.cs` (use classifier)

**To deprecate (not delete):**
- `src/MessengerWebhook/Services/Conversation/TopicAnalyzer.cs` (mark obsolete)

**Dependencies:**
- `src/MessengerWebhook/Services/SubIntent/ISubIntentClassifier.cs` (Phase 1)
- `src/MessengerWebhook/Services/SubIntent/SubIntentCategory.cs` (Phase 1)
- `src/MessengerWebhook/Services/SubIntent/ConversationContext.cs` (Phase 1)

## Implementation Steps

### 1. Inject ISubIntentClassifier into SalesStateHandlerBase (30min)

```csharp
// In SalesStateHandlerBase.cs constructor
protected readonly ISubIntentClassifier SubIntentClassifier;

protected SalesStateHandlerBase(
    // ... existing params ...
    ISubIntentClassifier subIntentClassifier,
    // ... rest of params ...
)
{
    // ... existing assignments ...
    SubIntentClassifier = subIntentClassifier;
}
```

### 2. Add BuildConversationContext helper (30min)

```csharp
// In SalesStateHandlerBase.cs
protected ConversationContext BuildConversationContext(ConversationStateData state)
{
    var recentHistory = state.History
        .TakeLast(5)
        .Select(h => new ConversationMessage
        {
            Role = h.Role,
            Content = h.Content,
            Timestamp = h.Timestamp
        })
        .ToList();

    // Get dominant topic from TopicAnalyzer (legacy)
    var topicAnalyzer = new TopicAnalyzer();
    var topics = topicAnalyzer.ExtractTopics(state.History);
    var dominantTopic = topics.FirstOrDefault()?.Name;

    return new ConversationContext
    {
        CurrentState = state.State.ToString(),
        HasProduct = state.SelectedProducts?.Any() ?? false,
        RecentHistory = recentHistory,
        DominantTopic = dominantTopic
    };
}
```

### 3. Modify ConsultingStateHandler to use classifier (45min)

```csharp
// In ConsultingStateHandler.cs
public override async Task<StateTransitionResult> HandleAsync(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken = default)
{
    // Build conversation context
    var context = BuildConversationContext(state);
    
    // Classify sub-intent
    var subIntent = await SubIntentClassifier.ClassifyAsync(message, context, cancellationToken);
    
    if (subIntent != null)
    {
        Logger.LogInformation(
            "Sub-intent classified: {Category} (confidence: {Confidence}, source: {Source})",
            subIntent.Category,
            subIntent.Confidence,
            subIntent.Source);
        
        // Route by sub-intent category
        var response = await RouteBySubIntent(subIntent, message, state, cancellationToken);
        
        if (response != null)
        {
            return StateTransitionResult.Stay(response);
        }
    }
    
    // Fallback to existing logic (TopicAnalyzer)
    Logger.LogDebug("Sub-intent classifier returned null, using fallback logic");
    return await HandleWithTopicAnalyzer(message, state, cancellationToken);
}

private async Task<string?> RouteBySubIntent(
    SubIntentResult subIntent,
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    return subIntent.Category switch
    {
        SubIntentCategory.ProductQuestion => await HandleProductQuestion(message, state, cancellationToken),
        SubIntentCategory.PriceQuestion => await HandlePriceQuestion(message, state, cancellationToken),
        SubIntentCategory.ShippingQuestion => await HandleShippingQuestion(message, state, cancellationToken),
        SubIntentCategory.PolicyQuestion => await HandlePolicyQuestion(message, state, cancellationToken),
        SubIntentCategory.AvailabilityQuestion => await HandleAvailabilityQuestion(message, state, cancellationToken),
        SubIntentCategory.ComparisonQuestion => await HandleComparisonQuestion(message, state, cancellationToken),
        _ => null // None or unknown
    };
}
```

### 4. Implement sub-intent handlers (15min each = 90min total)

```csharp
private async Task<string> HandleProductQuestion(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    // Use RAG service for product knowledge
    if (RagService != null && state.SelectedProducts?.Any() == true)
    {
        var product = state.SelectedProducts.First();
        var ragContext = await RagService.QueryAsync(message, product.Id, cancellationToken);
        
        // Generate response with RAG context
        return await GeminiService.GenerateResponseAsync(
            message,
            state.History,
            ragContext,
            cancellationToken);
    }
    
    // Fallback to general product info
    return await GeminiService.GenerateResponseAsync(message, state.History, cancellationToken);
}

private async Task<string> HandlePriceQuestion(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    // Get pricing info from selected products
    if (state.SelectedProducts?.Any() == true)
    {
        var product = state.SelectedProducts.First();
        var priceInfo = $"Giá sản phẩm {product.Name}: {product.Price:N0}đ";
        
        // Check for promotions
        if (product.DiscountPrice.HasValue)
        {
            priceInfo += $"\nGiá khuyến mãi: {product.DiscountPrice:N0}đ";
        }
        
        return await GeminiService.GenerateResponseAsync(
            message,
            state.History,
            priceInfo,
            cancellationToken);
    }
    
    return "Dạ chị cho em biết sản phẩm nào chị quan tâm để em báo giá chi tiết nha.";
}

private async Task<string> HandleShippingQuestion(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    // Get shipping info
    var shippingInfo = "Múi Xù giao hàng toàn quốc:\n" +
                      "- Nội thành HCM: 1-2 ngày\n" +
                      "- Tỉnh thành khác: 2-4 ngày\n" +
                      "- Freeship đơn từ 300k";
    
    return await GeminiService.GenerateResponseAsync(
        message,
        state.History,
        shippingInfo,
        cancellationToken);
}

private async Task<string> HandlePolicyQuestion(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    // Use policy guard service
    var policyResponse = await PolicyGuardService.CheckAsync(message, state, cancellationToken);
    
    if (policyResponse.ShouldEscalate)
    {
        return policyResponse.SafeReply;
    }
    
    return await GeminiService.GenerateResponseAsync(message, state.History, cancellationToken);
}

private async Task<string> HandleAvailabilityQuestion(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    // Check product availability
    if (state.SelectedProducts?.Any() == true)
    {
        var product = state.SelectedProducts.First();
        var available = product.StockQuantity > 0;
        var availabilityInfo = available
            ? $"Dạ sản phẩm {product.Name} hiện còn hàng ạ."
            : $"Dạ sản phẩm {product.Name} tạm hết hàng. Em sẽ thông báo khi có hàng trở lại nha.";
        
        return await GeminiService.GenerateResponseAsync(
            message,
            state.History,
            availabilityInfo,
            cancellationToken);
    }
    
    return "Dạ chị cho em biết sản phẩm nào chị muốn kiểm tra tình trạng hàng nha.";
}

private async Task<string> HandleComparisonQuestion(
    string message,
    ConversationStateData state,
    CancellationToken cancellationToken)
{
    // Use product mapping service to find products mentioned
    var products = await ProductMappingService.ExtractProductsAsync(message, cancellationToken);
    
    if (products.Count >= 2)
    {
        var comparisonInfo = BuildComparisonInfo(products);
        return await GeminiService.GenerateResponseAsync(
            message,
            state.History,
            comparisonInfo,
            cancellationToken);
    }
    
    return "Dạ chị muốn so sánh sản phẩm nào với sản phẩm nào ạ?";
}
```

## Todo List

- [ ] Inject `ISubIntentClassifier` into `SalesStateHandlerBase` constructor
- [ ] Add `BuildConversationContext` helper method
- [ ] Modify `ConsultingStateHandler.HandleAsync` to use classifier
- [ ] Implement `RouteBySubIntent` method
- [ ] Implement `HandleProductQuestion` (RAG integration)
- [ ] Implement `HandlePriceQuestion` (pricing info)
- [ ] Implement `HandleShippingQuestion` (shipping info)
- [ ] Implement `HandlePolicyQuestion` (policy guard)
- [ ] Implement `HandleAvailabilityQuestion` (stock check)
- [ ] Implement `HandleComparisonQuestion` (product comparison)
- [ ] Add logging for sub-intent classification
- [ ] Keep `TopicAnalyzer` as fallback (mark deprecated)
- [ ] Update all handler constructors to pass classifier
- [ ] Compile and verify no errors

## Success Criteria

- [ ] `ISubIntentClassifier` injected into all state handlers
- [ ] `ConversationContext` built correctly from state machine state
- [ ] All 6 sub-intent categories routed to appropriate handlers
- [ ] RAG service used for product questions
- [ ] Pricing service used for price questions
- [ ] Policy guard used for policy questions
- [ ] Fallback to `TopicAnalyzer` works when classifier returns null
- [ ] No breaking changes to existing handlers
- [ ] Logging shows sub-intent, confidence, source
- [ ] Compile succeeds, no DI errors

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking existing handlers | Medium | High | Keep TopicAnalyzer fallback, gradual rollout |
| DI registration errors | Low | High | Integration test verifies DI resolution |
| Performance regression | Low | Medium | Benchmark shows p95 <1s |
| RAG service not available | Low | Medium | Null check, fallback to Gemini only |

## Security Considerations

- No PII logged (message content at debug level only)
- Policy guard still enforces safety rules
- Sub-intent classification doesn't bypass security checks

## Next Steps

**Blocks:** Phase 7 (Testing)

**After completion:**
1. Manual testing with real Vietnamese queries
2. Verify all sub-intent categories route correctly
3. Proceed to Phase 7: Unit & integration tests
