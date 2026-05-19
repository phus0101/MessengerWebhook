# Phase 5: Conversation Flows

**Priority**: High
**Status**: Pending
**Duration**: 2 weeks
**Dependencies**: Phase 2 (Gemini Integration), Phase 2.5 (RAG Layer), Phase 3 (State Machine), Phase 4 (Product Catalog)

---

## Context Links

- Research: [Gemini API Report](../reports/researcher-260320-1042-gemini-api.md)
- Research: [Order Management Report](../reports/researcher-260320-1042-order-management.md)
- State Machine: [Phase 3 - State Machine](./phase-03-state-machine.md)
- Product Catalog: [Phase 4 - Product Catalog](./phase-04-product-catalog.md)

---

## Overview

Design AI-powered beauty consultation flows using Gemini Pro 3.1. Implement skin profile extraction, ingredient-based product matching, compatibility checking, and multi-turn consultation flows in Vietnamese. Uses RAG (Phase 2.5) for semantic product search.

---

## Key Insights

- PTCF Framework: Persona (beauty consultant), Task (skin analysis), Context (profile), Format (recommendations)
- System prompt loaded from file: beauty-consultant-system-prompt.txt
- Context window: 65,000 tokens (last 10 turns sent to API)
- Vietnamese language support confirmed
- Hybrid model: Pro 50% (skin consultation), Flash-Lite 50% (simple queries)
- Function calling for skin profile extraction
- Conversation history persisted (30-day retention)
- RAG integration for ingredient-based search

---

## Requirements

### Functional
- Extract skin profile: type (oily/dry/combination/sensitive), concerns (acne/aging/dryness)
- Detect user intent (consultation, search, track order, help)
- Provide ingredient-based product recommendations via RAG
- Check ingredient compatibility (warn contraindications)
- Ask clarifying questions about skin (max 2 per turn)
- Handle multi-turn beauty consultations
- Support order tracking queries
- Graceful fallback for unclear input
- Maintain conversation context + skin profile across turns

### Non-Functional
- Response time <1s (streaming)
- Natural Vietnamese conversation (beauty terminology)
- Context retention across 10+ turns
- Cost <$0.15 per conversation (Pro 50%)
- Skin profile extraction accuracy >90%
- Ingredient match relevance >85%
- Fallback to dermatologist when needed

---

## Architecture

### Conversation Flow Types

**1. Skin Consultation Flow** (Primary)
```
User: "Da tôi dầu và hay bị mụn"
  ↓
AI: Extract profile (skin_type: oily, concern: acne)
  ↓
AI: Ask clarifying questions (2 max)
  - "Bạn có dùng sản phẩm nào hiện tại không?"
  - "Da bạn có nhạy cảm với thành phần nào không?"
  ↓
User: Answers
  ↓
AI: Generate embedding → RAG search (BHA, niacinamide products)
  ↓
AI: Check compatibility → Recommend top 5 → Show carousel
```

**2. Ingredient Search Flow**
```
User: "Tìm sản phẩm có niacinamide"
  ↓
AI: RAG search by ingredient
  ↓
AI: Filter by user's skin profile (if exists)
  ↓
AI: Show products → Explain benefits
```

**3. Product Comparison Flow**
```
User: "So sánh 2 sản phẩm này"
  ↓
AI: Load products → Extract ingredients
  ↓
AI: Compare formulations, pH, texture
  ↓
AI: Recommend based on skin profile
```

**4. Order Tracking Flow**
```
User: "Đơn hàng của tôi đâu?"
  ↓
AI: Request order ID → Lookup → Explain status
```

**5. Help Flow**
```
User: "Làm sao để đổi size?" → AI: Explain return policy
  → Offer to create return request
```

### Intent Detection Strategy
```csharp
// Use Gemini with structured output
var intent = await DetectIntentAsync(message);
// Returns: { type: "browse", category: "shirts", confidence: 0.95 }
```

---

## Related Code Files

### To Create
- `src/MessengerWebhook/Services/AI/IIntentDetector.cs`
- `src/MessengerWebhook/Services/AI/IntentDetector.cs`
- `src/MessengerWebhook/Services/AI/Models/IntentResult.cs`
- `src/MessengerWebhook/Services/AI/Prompts/SystemPrompts.cs`
- `src/MessengerWebhook/Services/AI/Prompts/PromptTemplates.cs`
- `src/MessengerWebhook/Services/Conversation/IConversationManager.cs`
- `src/MessengerWebhook/Services/Conversation/ConversationManager.cs`
- `src/MessengerWebhook/Services/Conversation/ContextSummarizer.cs`
- `src/MessengerWebhook/StateMachine/Handlers/ConsultationHandler.cs`
- `src/MessengerWebhook/StateMachine/Handlers/OrderTrackingHandler.cs`
- `src/MessengerWebhook/StateMachine/Handlers/HelpHandler.cs`

### To Modify
- `src/MessengerWebhook/Services/AI/GeminiService.cs` (add function calling)
- `src/MessengerWebhook/StateMachine/Handlers/BrowsingStateHandler.cs` (enhance with AI)
- `src/MessengerWebhook/StateMachine/Handlers/GreetingStateHandler.cs` (add intent detection)

---

## Implementation Steps

### 1. Define Intent Types
```csharp
public enum IntentType
{
    Unknown,
    Greeting,
    Browse,
    Search,
    ProductInquiry,
    OrderTracking,
    Help,
    Consultation,
    AddToCart,
    Checkout,
    CancelOrder
}

public class IntentResult
{
    public IntentType Type { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, string> Entities { get; set; } = new();
    // Entities: { "category": "shirts", "color": "white", "occasion": "formal" }
}
```

### 2. Create System Prompts
```csharp
public static class SystemPrompts
{
    public const string SalesConsultant = @"
Bạn là chuyên viên tư vấn thời trang chuyên nghiệp cho cửa hàng quần áo.

NHIỆM VỤ:
- Giúp khách hàng tìm sản phẩm phù hợp với phong cách, dáng người, và dịp sử dụng
- Đặt câu hỏi làm rõ nhu cầu (tối đa 2 câu hỏi mỗi lần)
- Gợi ý sản phẩm cụ thể khi đã hiểu rõ nhu cầu
- Hỗ trợ theo dõi đơn hàng và giải đáp thắc mắc

PHONG CÁCH:
- Thân thiện, chuyên nghiệp, nhiệt tình
- Trả lời ngắn gọn (2-3 câu), dễ đọc trên điện thoại
- Sử dụng emoji phù hợp (không lạm dụng)
- Tránh thuật ngữ phức tạp

GIỚI HẠN:
- Chỉ giới thiệu sản phẩm có trong danh mục
- Không đưa ra lời khuyên y tế hoặc pháp lý
- Không thảo luận về giá cả của đối thủ
- Không hứa hẹn điều không chắc chắn

DANH MỤC SẢN PHẨM:
- Áo sơ mi, áo thun, áo polo
- Quần jean, quần kaki, quần short
- Váy, đầm
- Giày, dép
- Phụ kiện: túi, ví, thắt lưng
";

    public const string IntentDetection = @"
Phân tích ý định của khách hàng từ tin nhắn.

TRẢ VỀ JSON với format:
{
  ""intent"": ""browse|search|order_tracking|help|consultation"",
  ""confidence"": 0.0-1.0,
  ""entities"": {
    ""category"": ""shirts|pants|dresses|shoes|accessories"",
    ""color"": ""đỏ|xanh|trắng|đen|..."",
    ""occasion"": ""casual|formal|party|sport"",
    ""order_id"": ""..."",
    ""product_name"": ""...""
  }
}

VÍ DỤ:
- ""Tôi muốn xem áo sơ mi"" → {""intent"": ""browse"", ""entities"": {""category"": ""shirts""}}
- ""Đơn hàng #12345 đến đâu rồi?"" → {""intent"": ""order_tracking"", ""entities"": {""order_id"": ""12345""}}
- ""Tư vấn trang phục dự tiệc"" → {""intent"": ""consultation"", ""entities"": {""occasion"": ""party""}}
";
}
```

### 3. Implement IIntentDetector
```csharp
public interface IIntentDetector
{
    Task<IntentResult> DetectAsync(string message, StateContext context);
}
```

### 4. Implement IntentDetector
```csharp
public class IntentDetector : IIntentDetector
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<IntentDetector> _logger;

    public async Task<IntentResult> DetectAsync(string message, StateContext context)
    {
        // Use Gemini with JSON mode for structured output
        var prompt = $@"{SystemPrompts.IntentDetection}

TIN NHẮN: {message}

NGỮ CẢNH: {GetContextSummary(context)}";

        try
        {
            var response = await _geminiService.SendMessageAsync(
                context.FacebookPSID,
                prompt,
                new List<ConversationMessage>(),
                GeminiModelType.FlashLite);

            // Parse JSON response
            var intent = JsonSerializer.Deserialize<IntentResult>(response);
            return intent ?? new IntentResult { Type = IntentType.Unknown };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Intent detection failed");
            return FallbackIntentDetection(message);
        }
    }

    private IntentResult FallbackIntentDetection(string message)
    {
        // Simple keyword-based fallback
        var lower = message.ToLower();

        if (lower.Contains("đơn hàng") || lower.Contains("order"))
            return new IntentResult { Type = IntentType.OrderTracking, Confidence = 0.7 };

        if (lower.Contains("tư vấn") || lower.Contains("gợi ý"))
            return new IntentResult { Type = IntentType.Consultation, Confidence = 0.7 };

        if (lower.Contains("xem") || lower.Contains("mua"))
            return new IntentResult { Type = IntentType.Browse, Confidence = 0.7 };

        return new IntentResult { Type = IntentType.Unknown, Confidence = 0.5 };
    }

    private string GetContextSummary(StateContext context)
    {
        var summary = $"Trạng thái: {context.CurrentState}";
        if (context.Data.ContainsKey("selectedProduct"))
            summary += $", Đang xem sản phẩm";
        if (context.Data.ContainsKey("cartId"))
            summary += $", Có giỏ hàng";
        return summary;
    }
}
```

### 5. Implement Conversation Manager
```csharp
public interface IConversationManager
{
    Task<string> GenerateResponseAsync(StateContext context, string userMessage, IntentResult intent);
    Task<List<ConversationMessage>> GetManagedHistoryAsync(StateContext context);
}

public class ConversationManager : IConversationManager
{
    private readonly IGeminiService _geminiService;
    private readonly IContextSummarizer _summarizer;
    private const int MaxHistoryTurns = 5;

    public async Task<string> GenerateResponseAsync(
        StateContext context,
        string userMessage,
        IntentResult intent)
    {
        // Get managed history (last 5 turns + summary)
        var history = await GetManagedHistoryAsync(context);

        // Build context-aware prompt
        var systemPrompt = BuildSystemPrompt(context, intent);

        // Select model based on complexity
        var modelType = intent.Type == IntentType.Consultation
            ? GeminiModelType.Pro
            : GeminiModelType.FlashLite;

        // Generate response
        var response = await _geminiService.SendMessageAsync(
            context.FacebookPSID,
            userMessage,
            history,
            modelType);

        // Update history
        context.History.Add(new ConversationMessage { Role = "user", Content = userMessage });
        context.History.Add(new ConversationMessage { Role = "model", Content = response });

        return response;
    }

    public async Task<List<ConversationMessage>> GetManagedHistoryAsync(StateContext context)
    {
        if (context.History.Count <= MaxHistoryTurns * 2)
        {
            return context.History;
        }

        // Summarize old messages
        var oldMessages = context.History.Take(context.History.Count - MaxHistoryTurns * 2).ToList();
        var summary = await _summarizer.SummarizeAsync(oldMessages);

        // Keep recent messages + summary
        var managedHistory = new List<ConversationMessage>
        {
            new() { Role = "user", Content = $"[Tóm tắt cuộc trò chuyện trước: {summary}]" }
        };
        managedHistory.AddRange(context.History.Skip(context.History.Count - MaxHistoryTurns * 2));

        return managedHistory;
    }

    private string BuildSystemPrompt(StateContext context, IntentResult intent)
    {
        var prompt = SystemPrompts.SalesConsultant;

        // Add context-specific instructions
        if (context.CurrentState == ConversationState.ProductView)
        {
            var product = context.GetData<ProductDto>("selectedProduct");
            prompt += $"\n\nKHÁCH ĐANG XEM: {product?.Name} - {product?.BasePrice:N0} VNĐ";
        }

        if (context.Data.ContainsKey("cartId"))
        {
            prompt += "\n\nKhách đã có sản phẩm trong giỏ hàng.";
        }

        return prompt;
    }
}
```

### 6. Implement Context Summarizer
```csharp
public class ContextSummarizer : IContextSummarizer
{
    private readonly IGeminiService _geminiService;

    public async Task<string> SummarizeAsync(List<ConversationMessage> messages)
    {
        var conversation = string.Join("\n", messages.Select(m =>
            $"{(m.Role == "user" ? "Khách" : "Bot")}: {m.Content}"));

        var prompt = $@"Tóm tắt cuộc trò chuyện sau trong 1-2 câu, tập trung vào:
- Sản phẩm khách quan tâm
- Yêu cầu và sở thích của khách
- Quyết định đã đưa ra

CUỘC TRÒ CHUYỆN:
{conversation}

TÓM TẮT:";

        return await _geminiService.SendMessageAsync(
            "system",
            prompt,
            new List<ConversationMessage>(),
            GeminiModelType.FlashLite);
    }
}
```

### 7. Implement Consultation Handler
```csharp
public class ConsultationHandler : IStateHandler
{
    private readonly IConversationManager _conversationManager;
    private readonly IProductService _productService;
    private readonly ITemplateBuilder _templateBuilder;
    private readonly IMessengerService _messengerService;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        // Generate AI response with product recommendations
        var response = await _conversationManager.GenerateResponseAsync(
            context,
            message,
            new IntentResult { Type = IntentType.Consultation });

        // Extract product recommendations from context
        if (ShouldShowProducts(context))
        {
            var recommendations = await GetRecommendationsAsync(context);
            if (recommendations.Any())
            {
                var template = _templateBuilder.BuildProductCarousel(recommendations);
                await _messengerService.SendTemplateAsync(context.FacebookPSID, template);
            }
        }

        return response;
    }

    private bool ShouldShowProducts(StateContext context)
    {
        // Show products after 2-3 clarifying questions
        var userMessages = context.History.Count(m => m.Role == "user");
        return userMessages >= 2 && context.GetData<string>("occasion") != null;
    }

    private async Task<List<ProductDto>> GetRecommendationsAsync(StateContext context)
    {
        var occasion = context.GetData<string>("occasion");
        var category = context.GetData<string>("category");

        // Use product service to get recommendations
        if (!string.IsNullOrEmpty(category))
        {
            return await _productService.GetByCategoryAsync(category);
        }

        return await _productService.GetFeaturedAsync();
    }
}
```

### 8. Implement Order Tracking Handler
```csharp
public class OrderTrackingHandler : IStateHandler
{
    private readonly IOrderRepository _orderRepo;
    private readonly IConversationManager _conversationManager;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        // Extract order ID from message
        var orderIdMatch = Regex.Match(message, @"#?(\d+)");
        if (!orderIdMatch.Success)
        {
            return "Vui lòng cung cấp mã đơn hàng của bạn (ví dụ: #12345)";
        }

        var orderId = orderIdMatch.Groups[1].Value;
        var order = await _orderRepo.GetByIdAsync(int.Parse(orderId));

        if (order == null)
        {
            return $"Không tìm thấy đơn hàng #{orderId}. Vui lòng kiểm tra lại mã đơn hàng.";
        }

        // Generate natural language status explanation
        var statusPrompt = $@"Giải thích trạng thái đơn hàng cho khách hàng:

Mã đơn hàng: #{order.OrderId}
Trạng thái: {order.Status}
Ngày đặt: {order.CreatedAt:dd/MM/yyyy}
Tổng tiền: {order.TotalAmount:N0} VNĐ

Hãy giải thích ngắn gọn, thân thiện.";

        var response = await _conversationManager.GenerateResponseAsync(
            context,
            statusPrompt,
            new IntentResult { Type = IntentType.OrderTracking });

        return response;
    }
}
```

### 9. Update Greeting Handler with Intent Detection
```csharp
public class GreetingStateHandler : IStateHandler
{
    private readonly IIntentDetector _intentDetector;
    private readonly IConversationManager _conversationManager;
    private readonly IStateMachine _stateMachine;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        // Detect intent
        var intent = await _intentDetector.DetectAsync(message, context);

        // Generate greeting response
        var response = await _conversationManager.GenerateResponseAsync(context, message, intent);

        // Transition based on intent
        switch (intent.Type)
        {
            case IntentType.Browse:
            case IntentType.Search:
                await _stateMachine.TransitionAsync(context, ConversationState.Browsing);
                break;

            case IntentType.OrderTracking:
                await _stateMachine.TransitionAsync(context, ConversationState.OrderTracking);
                break;

            case IntentType.Consultation:
                await _stateMachine.TransitionAsync(context, ConversationState.Browsing);
                context.SetData("consultationMode", true);
                break;
        }

        return response;
    }
}
```

### 10. Register Services in Program.cs
```csharp
// Conversation services
builder.Services.AddScoped<IIntentDetector, IntentDetector>();
builder.Services.AddScoped<IConversationManager, ConversationManager>();
builder.Services.AddScoped<IContextSummarizer, ContextSummarizer>();

// Enhanced state handlers
builder.Services.AddScoped<ConsultationHandler>();
builder.Services.AddScoped<OrderTrackingHandler>();
```

### 11. Write Unit Tests
```csharp
[Fact]
public async Task DetectAsync_BrowseIntent_ReturnsCorrectIntent()
{
    var result = await _intentDetector.DetectAsync("Tôi muốn xem áo sơ mi", context);
    Assert.Equal(IntentType.Browse, result.Type);
    Assert.True(result.Confidence > 0.7);
}

[Fact]
public async Task GenerateResponseAsync_ConsultationIntent_UsesPro()
{
    var intent = new IntentResult { Type = IntentType.Consultation };
    var response = await _conversationManager.GenerateResponseAsync(context, "Tư vấn", intent);
    Assert.NotEmpty(response);
    // Verify Pro model was used
}

[Fact]
public async Task GetManagedHistoryAsync_LongHistory_Summarizes()
{
    // Add 20 messages to history
    for (int i = 0; i < 20; i++)
    {
        context.History.Add(new ConversationMessage { Role = "user", Content = $"Message {i}" });
    }

    var managed = await _conversationManager.GetManagedHistoryAsync(context);
    Assert.True(managed.Count < context.History.Count);
    Assert.Contains("Tóm tắt", managed[0].Content);
}
```

### 12. Integration Testing
Test complete conversation flows:
- Product discovery (3-4 turns)
- Consultation (5-6 turns with recommendations)
- Order tracking (2-3 turns)
- Context retention across turns
- Vietnamese language quality

---

## Todo List

- [ ] Define IntentType enum and IntentResult model
- [ ] Create system prompts for different scenarios
- [ ] Implement IIntentDetector interface
- [ ] Implement IntentDetector with Gemini
- [ ] Create fallback intent detection (keyword-based)
- [ ] Implement IConversationManager interface
- [ ] Implement ConversationManager with history management
- [ ] Implement ContextSummarizer
- [ ] Implement ConsultationHandler
- [ ] Implement OrderTrackingHandler
- [ ] Update GreetingStateHandler with intent detection
- [ ] Update BrowsingStateHandler with AI recommendations
- [ ] Register all services in DI container
- [ ] Write unit tests for intent detection
- [ ] Write unit tests for conversation manager
- [ ] Integration test full conversation flows
- [ ] Test Vietnamese language quality
- [ ] Optimize prompts based on testing

---

## Success Criteria

- Intent detection accuracy >85%
- Natural Vietnamese responses
- Context retained across 10+ turns
- Consultation flow provides relevant recommendations
- Order tracking works correctly
- Response time <1s (streaming)
- Cost <$0.10 per conversation
- Unit tests pass (100% coverage)
- Integration tests pass for all flows

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Poor Vietnamese language quality | Low | High | Test extensively, adjust prompts |
| Intent detection errors | Medium | Medium | Implement fallback, clarifying questions |
| High API costs | Medium | Medium | Use hybrid model, context summarization |
| Context loss in long conversations | Low | Medium | Implement summarization |

---

## Security Considerations

- Sanitize user input before sending to Gemini
- Don't send sensitive data (passwords, payment info) to AI
- Validate AI responses before displaying
- Rate limit per user to prevent abuse
- Log conversations for quality monitoring (anonymized)

---

## Prompt Engineering Tips

**Effective Prompts:**
- Clear persona and role definition
- Specific constraints and boundaries
- Examples for complex tasks
- Output format specification
- Context inclusion

**Avoid:**
- Vague instructions
- Overly long prompts (>1000 tokens)
- Contradictory requirements
- Assuming knowledge not provided

---

## Next Steps

After Phase 5 completion:
1. Proceed to Phase 6: Order Workflow
2. Integrate conversation flows into all state handlers
3. Test with real users for feedback
4. Optimize prompts based on usage data
5. Monitor API costs and adjust model selection
