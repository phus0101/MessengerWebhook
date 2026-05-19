# Phase 04: 3-Layer Context Window Management

**Priority**: P1  
**Effort**: 2-3 ngày  
**Status**: Complete  
**Depends on**: Phase 01 (DI fix)

---

## Vấn đề

Research doc: *"Không nên giữ nguyên toàn bộ lịch sử chat trong prompt. Tách thành 3 lớp: ephemeral turn history, conversation summary, retrieval context."*

Hiện tại `ConversationHistoryHelper` chỉ giữ N turns gần nhất (simple slice). Khi conversation dài:
- Token cost tăng tuyến tính
- LLM "mất" context từ đầu phiên (product intent ban đầu, contact info collected)
- Không có summary → khi cắt turns cũ, business context bị mất

---

## Mục tiêu

1. **Conversation Summarizer** — Khi history > threshold, tóm tắt các turns cũ thành 1 đoạn ngắn
2. **3-layer context assembly** — Ephemeral (last N turns) + Summary + Retrieval context
3. **Business state preservation** — Summary phải capture: product đã chọn, contact info đã thu, intent của khách
4. **Không thay đổi StateContext schema** — Summary lưu trong `ctx.Data["conversationSummary"]`

---

## Thiết kế

### 3 Layers

```
┌─────────────────────────────────────────┐
│ Layer 1: Retrieval Context (RAG)         │ ← đã có, đứng đầu prompt
├─────────────────────────────────────────┤
│ Layer 2: Conversation Summary            │ ← MỚI: tóm tắt turns cũ
│ "Khách hỏi kem chống nắng SPF50,        │
│  đã xác nhận SĐT 09xx, chưa có địa chỉ" │
├─────────────────────────────────────────┤
│ Layer 3: Ephemeral (last 6 turns)        │ ← giữ nguyên, giảm từ N → 6
└─────────────────────────────────────────┘
```

### Conversation Summarizer

```csharp
public interface IConversationSummarizer
{
    // Tóm tắt turns cũ (trước last K turns) thành summary ngắn
    Task<string> SummarizeAsync(
        IReadOnlyList<ConversationMessage> olderTurns,
        string? existingSummary,  // rolling summary từ lần trước
        CancellationToken ct = default);
}
```

Trigger khi: `history.Count > SummarizationThreshold` (mặc định 10 turns).

Summary format (structured, không free-text để LLM follow):
```
[Tóm tắt phiên]
Sản phẩm quan tâm: {productCodes hoặc "chưa rõ"}
Liên hệ đã thu: SĐT={phone}, Địa chỉ={address}
Intent khách: {Consulting/ReadyToBuy/...}
Điểm đặc biệt: {objections, gifts, discount đã đề cập}
```

### Khi nào tóm tắt

```csharp
// Trong ConversationHistoryHelper.AddToHistory():
if (history.Count > options.SummarizationThreshold)
{
    // Lấy turns cũ (trước last EphemeralWindowSize turns)
    var olderTurns = history.Take(history.Count - EphemeralWindowSize).ToList();
    // Tóm tắt async (fire-and-forget hoặc await?)
}
```

**Quyết định**: Summarize **synchronously** khi threshold vượt, không fire-and-forget. Latency tăng ~500ms chỉ tại turn summarization, không phải mọi turn.

### Prompt assembly

`SalesPromptBuilder` cần inject summary vào prompt:

```csharp
public string BuildSystemPrompt(StateContext ctx, string ragContext)
{
    var summary = ctx.GetData<string>("conversationSummary");
    
    return $"""
        {_systemPolicy}
        {_brandVoice}
        
        [BỐI CẢNH PHIÊN]
        {summary ?? "Phiên mới, chưa có lịch sử."}
        
        [THÔNG TIN SẢN PHẨM VÀ CHÍNH SÁCH]
        {ragContext}
        """;
}
```

---

## Config

```json
// appsettings.json → SalesBotOptions
"SalesBot": {
    "ConversationHistoryLimit": 20,    // giữ nguyên cho compatibility
    "EphemeralWindowSize": 6,          // turns gần nhất trong prompt
    "SummarizationThreshold": 10,      // trigger tóm tắt khi > 10 turns
    "SummarizationEnabled": true       // feature flag để rollback
}
```

---

## Files cần tạo

- `Services/Conversation/IConversationSummarizer.cs`
- `Services/Conversation/ConversationSummarizer.cs` — gọi Gemini FlashLite để tóm tắt
- `Services/Conversation/Models/ConversationSummary.cs` — typed model cho summary

## Files cần sửa

- `Configuration/SalesBotOptions.cs` — thêm `EphemeralWindowSize`, `SummarizationThreshold`, `SummarizationEnabled`
- `Utilities/ConversationHistoryHelper.cs` — trigger summarization logic
- `Services/Sales/Prompt/SalesPromptBuilder.cs` — inject summary vào prompt
- `Configuration/ServiceRegistration/AiServicesRegistration.cs` — đăng ký IConversationSummarizer

---

## Implementation Steps

### Step 1: Tạo ConversationSummarizer (1 ngày)

```csharp
public class ConversationSummarizer : IConversationSummarizer
{
    private readonly IGeminiService _gemini;
    
    public async Task<string> SummarizeAsync(
        IReadOnlyList<ConversationMessage> olderTurns,
        string? existingSummary,
        CancellationToken ct)
    {
        var prompt = BuildSummaryPrompt(olderTurns, existingSummary);
        // Dùng FlashLite model (nhanh, rẻ cho summarization)
        return await _gemini.SendMessageAsync(
            "system", prompt, new(), GeminiModelType.FlashLite, ct);
    }
}
```

Prompt cho summarization phải structured để output predictable:
```
Tóm tắt cuộc hội thoại bán hàng sau đây thành tối đa 5 dòng.
Phải ghi rõ: sản phẩm đã đề cập, liên hệ đã cung cấp, trạng thái đơn hàng.
Nếu có summary cũ, merge với thông tin mới.
---
{existingSummary}
---
{olderTurns formatted}
```

### Step 2: Tích hợp vào ConversationHistoryHelper (0.5 ngày)

**Không sửa signature hiện tại** — thêm overload với summarizer:

```csharp
public static async Task AddToHistoryWithSummaryAsync(
    StateContext ctx,
    string role,
    string content,
    int historyLimit,
    int ephemeralWindowSize,
    int summarizationThreshold,
    IConversationSummarizer summarizer,
    CancellationToken ct = default)
```

Handlers dùng overload mới thông qua `SalesStateHandlerBase`.

### Step 3: Sửa SalesPromptBuilder (0.5 ngày)

Thêm `BuildContextualSystemPrompt(StateContext ctx, string ragContext)` method dùng summary.

### Step 4: Feature flag + Tests (0.5 ngày)

Nếu `SummarizationEnabled = false` → dùng path cũ (ConversationHistoryHelper đơn giản). Cho phép rollback không cần redeploy.

Unit test:
- Summary được tạo khi history > threshold
- Summary được lưu vào ctx.Data
- Ephemeral window = last 6 turns trong prompt
- Nếu disabled → behavior giống cũ

---

## Todo

- [ ] Thêm config fields vào SalesBotOptions
- [ ] Tạo IConversationSummarizer + ConversationSummarizer
- [ ] Tạo ConversationSummary model
- [ ] Thêm overload AddToHistoryWithSummaryAsync
- [ ] Sửa SalesStateHandlerBase dùng overload mới
- [ ] Sửa SalesPromptBuilder inject summary vào prompt
- [ ] Đăng ký IConversationSummarizer trong DI
- [ ] Unit test summarization trigger
- [ ] Unit test prompt assembly với summary
- [ ] Build + tests pass

---

## Success Criteria

- Khi history > 10 turns → `conversationSummary` xuất hiện trong `ctx.Data`
- Prompt gửi lên Gemini chứa summary section
- Ephemeral window = 6 turns gần nhất
- Khi `SummarizationEnabled = false` → behavior giống cũ hoàn toàn
- 0 regression trong unit/integration tests

---

## Risk

- **Summarization latency**: Gọi Gemini thêm 1 lần mỗi khi threshold vượt. Chỉ xảy ra tại 1 turn, không phải mọi turn. Acceptable
- **Summary chứa sai thông tin**: Gemini có thể hallucinate trong summary. Mitigation: structured prompt, validate output có đủ fields
- **ConversationHistoryHelper hiện tại**: Được gọi ở nhiều chỗ. Thêm overload async thay vì sửa sync method hiện tại → backward compatible
- **StateContext size**: Summary thêm vào ctx.Data được serialize vào DB — cần giữ ngắn (≤ 500 chars)
