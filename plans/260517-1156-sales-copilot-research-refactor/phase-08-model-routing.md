# Phase 08: Model Tiering & Routing

**Priority**: P1
**Effort**: 1.5-2 ngày
**Status**: Complete
**Depends on**: Phase 01 (DI fix) + Phase 02 (resilience pipeline)

---

## Vấn đề

Research doc: *"dùng model nhỏ cho intent classification và FAQ đơn giản; chỉ reroute sang model lớn khi confidence thấp hoặc ticket có giá trị cao"*.

Hệ thống hiện tại default 1 model cho mọi turn → cost không tối ưu, không tận dụng được Flash vs Pro pricing differential:
- Gemini 2.5 Flash: $0.30/1M input, $2.50/1M output
- Gemini 2.5 Pro: $1.25/1M input, $10/1M output (5× và 4× đắt hơn)
- Gemini 2.5 FlashLite: rẻ nhất, đủ cho summarization/classification

---

## Mục tiêu

1. **Routing decision framework** — map (intent, confidence, ticket_value, state) → model tier
2. **3 tiers**: FlashLite (classify/summarize) → Flash (default chat) → Pro (high-value/low-confidence)
3. **Cost tracking per tier** — emit metric `LlmCallCompleted Tier={tier}` để Seq aggregate
4. **Fallback chain** — nếu Pro fail → Flash → FlashLite → degraded response (Phase 02 fallback)

---

## Thiết kế

### Decision Matrix

| Scenario | Tier | Reasoning |
|---|---|---|
| Intent classification, sub-intent classification | FlashLite | Latency-sensitive, low complexity |
| Conversation summarization (Phase 04) | FlashLite | Structured output, no creativity needed |
| Small talk, greeting, simple FAQ (sub-intent: PolicyQuestion + cache miss) | Flash | Default chat |
| Product consultation (Consulting state, has product context) | Flash | Default |
| High-value ticket (draft order ≥ threshold VND, or VIP customer) | Pro | Worth the cost |
| Low confidence AI intent (`Confidence < 0.6`) | Pro | Better reasoning |
| Multi-turn complex policy question (`history.Count > 8` + objection signals) | Pro | Long-context |
| Human handoff candidate (escalation triggers) | Skip LLM | Direct degraded reply |

### Routing Service

```csharp
public interface ILlmRoutingService
{
    GeminiModelType SelectModel(LlmRoutingContext context);
}

public record LlmRoutingContext
{
    public CommerceMsgIntent Intent { get; init; }
    public ConversationState State { get; init; }
    public int HistoryTurnCount { get; init; }
    public decimal? EstimatedTicketValue { get; init; }
    public bool IsVipCustomer { get; init; }
    public string Purpose { get; init; } = "chat"; // "chat", "classify", "summarize"
}

public class LlmRoutingService : ILlmRoutingService
{
    private readonly LlmRoutingOptions _options;

    public GeminiModelType SelectModel(LlmRoutingContext ctx)
    {
        // Purpose-based override (highest priority)
        if (ctx.Purpose is "classify" or "summarize")
            return GeminiModelType.FlashLite;

        // High-value: Pro
        if (ctx.IsVipCustomer ||
            (ctx.EstimatedTicketValue ?? 0) >= _options.ProTierMinTicketValue ||
            ctx.Intent.Confidence < _options.LowConfidenceThreshold ||
            ctx.HistoryTurnCount > _options.LongConversationThreshold)
            return GeminiModelType.Pro;

        // Default
        return GeminiModelType.Flash;
    }
}
```

### Config

```json
"LlmRouting": {
    "Enabled": true,
    "LowConfidenceThreshold": 0.6,
    "LongConversationThreshold": 8,
    "ProTierMinTicketValueVnd": 1000000,
    "FallbackChain": ["Pro", "Flash", "FlashLite"]
}
```

### Tích hợp với SalesReplyOrchestrator

```csharp
// Trước khi gọi Gemini, hỏi router
var routingCtx = new LlmRoutingContext
{
    Intent = msgIntent,
    State = ctx.CurrentState,
    HistoryTurnCount = history.Count,
    EstimatedTicketValue = draftEstimate,
    IsVipCustomer = vipProfile?.IsVip == true,
    Purpose = "chat"
};
var modelTier = _router.SelectModel(routingCtx);

var response = await _gemini.SendMessageAsync(
    role, prompt, options, modelTier, ct);
```

### Observability

```csharp
Logger.LogInformation(
    "LlmCallCompleted Tier={Tier} Model={Model} ElapsedMs={Elapsed} " +
    "InputTokens={Input} OutputTokens={Output} EstCostUsd={Cost} TenantId={TenantId}",
    modelTier, modelName, elapsed, inputTokens, outputTokens, estCost, tenantId);
```

Seq saved query — daily cost breakdown per tier:
```
@MessageTemplate startswith 'LlmCallCompleted'
| summarize TotalCost = sum(EstCostUsd), Count = count() by Tier, bin(@Timestamp, 1d)
```

---

## Files cần tạo

- `Services/AI/Routing/ILlmRoutingService.cs`
- `Services/AI/Routing/LlmRoutingService.cs`
- `Services/AI/Routing/LlmRoutingContext.cs`
- `Configuration/LlmRoutingOptions.cs`

## Files cần sửa

- `Services/Sales/Reply/SalesReplyOrchestrator.cs` — inject `ILlmRoutingService`, call `SelectModel` before LLM call
- `Services/AI/GeminiService.cs` — accept `GeminiModelType` parameter (verify hiện tại có support chưa)
- `Services/Conversation/ConversationSummarizer.cs` (Phase 04) — Purpose="summarize"
- `Services/AI/IntentDetectionService.cs` — Purpose="classify"
- `Services/SubIntent/SubIntentClassifier.cs` — Purpose="classify"
- `Configuration/ServiceRegistration/AiServicesRegistration.cs` — register router + bind LlmRoutingOptions
- `appsettings.json` — thêm `LlmRouting` section

---

## Implementation Steps

### Step 1: Verify GeminiService model parameter support (0.25 ngày)

```bash
grep -n "GeminiModelType\|SendMessageAsync" src/MessengerWebhook/Services/AI/GeminiService.cs
```

Nếu chưa support per-call model selection → extend signature trước.

### Step 2: ILlmRoutingService + LlmRoutingService (0.5 ngày)

Implement decision matrix theo Thiết kế trên. Pure logic, không I/O ⇒ dễ unit test.

### Step 3: Inject vào caller sites (0.5 ngày)

- `SalesReplyOrchestrator` — main chat
- `ConversationSummarizer` (Phase 04) — luôn FlashLite
- `IntentDetectionService` — luôn FlashLite  
- `SubIntentClassifier` — luôn FlashLite

### Step 4: Observability (0.25 ngày)

Log `LlmCallCompleted` với Tier field. Update Seq baseline queries từ Phase 00.

### Step 5: Tests (0.5 ngày)

Unit test routing decision matrix với 8+ scenarios (mỗi row trong matrix table).
Integration test: VIP customer → Pro; cold customer + simple question → Flash; intent classification → FlashLite.

---

## Todo

- [ ] Verify GeminiService support per-call model selection
- [ ] Tạo ILlmRoutingService + LlmRoutingService
- [ ] Tạo LlmRoutingContext record
- [ ] Tạo LlmRoutingOptions + appsettings.json
- [ ] Inject router vào SalesReplyOrchestrator
- [ ] Force FlashLite cho ConversationSummarizer, IntentDetectionService, SubIntentClassifier
- [ ] Log LlmCallCompleted với Tier field
- [ ] Unit test decision matrix
- [ ] Integration test VIP + non-VIP scenarios
- [ ] Build + tests pass

---

## Success Criteria

- 3 model tiers active trong production
- ≥ 70% requests dùng Flash hoặc FlashLite (cost optimization)
- < 30% requests escalate Pro (high-value only)
- Seq dashboard hiển thị cost breakdown per tier
- 0 regression trong conversation quality (golden test pass)

---

## Risk

- **Pro escalation quá nhiều**: Tune `LowConfidenceThreshold` + `LongConversationThreshold` sau 1 tuần baseline
- **VIP customer flag accuracy**: Phụ thuộc `VipProfile.IsVip` — verify VipDetectionService chính xác
- **Model fallback cascade**: Pro fail → Flash; Flash fail → FlashLite; FlashLite fail → degraded (Phase 02) — đảm bảo không silent regression
- **Cost estimation accuracy**: `EstCostUsd` dùng pricing table hardcoded — Gemini có thể đổi giá, schedule quarterly review

---

## Notes

Research recommendation cụ thể:
> *"Nếu phải chốt một khuyến nghị duy nhất: hãy xây orchestrator + router + RAG + tool layer thật sạch ngay từ đầu, vì đó là phần khó thay đổi nhất."*

Router là 1 trong 4 trụ cột của orchestration layer. Phase này ship trước Phase 09 (RAG reranking) để có chỗ đứng routing rồi mới layer rerank.
