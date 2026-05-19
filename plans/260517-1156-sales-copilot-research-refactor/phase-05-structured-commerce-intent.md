# Phase 05: Structured Commerce Intent Extraction

**Priority**: P2  
**Effort**: 2-3 ngày  
**Status**: Complete  
**Depends on**: Phase 01 (DI fix — cần constructor gọn trước khi refactor logic)

---

## Vấn đề

`HandleSalesConversationAsync` trong `SalesStateHandlerBase` có ~15 boolean flags được tính độc lập bằng `ContainsAnyPhrase`:

```csharp
var isProductQuestion    = SalesMessageParser.ContainsAnyPhrase(message, "nói thêm", ...);
var isShippingQuestion   = SalesMessageParser.ContainsAnyPhrase(message, "freeship", ...);
var isPolicyQuestion     = isShippingQuestion || ContainsAnyPhrase(message, "quà gì", ...);
var isPriceQuestion      = SalesMessageParser.ContainsAnyPhrase(message, "giá bao nhiêu", ...);
var isInventoryQuestion  = SalesMessageParser.ContainsAnyPhrase(message, "còn hàng", ...);
// ... 10 boolean nữa
```

Vấn đề:
1. **Brittle**: Thiếu từ khoá tiếng Việt → miss intent. Thừa từ → false positive
2. **Conflict logic**: Chạy song song với AI intent detection → 2 nguồn không nhất quán
3. **Maintenance cost cao**: Thêm intent mới phải thêm từ khoá + logic rẽ nhánh
4. **Không structured**: Research doc yêu cầu `intent, sku[], qty[], coupon, shipping_address, contact, consent` là single structured output

---

## Mục tiêu

1. **Single-pass intent extraction** — 1 AI call trả về `CommerceMsgIntent` struct thay vì N boolean
2. **Keyword fast path giữ lại cho p99 <50ms cases** — Không xóa hoàn toàn keyword matching, chỉ consolidate
3. **Branching logic đơn giản hơn** — `HandleSalesConversationAsync` từ ~400 lines → ~200 lines
4. **Không thay đổi business invariants** — Grounding, confirmation flow, draft order logic giữ nguyên

---

## Thiết kế

### CommerceMsgIntent (structured output)

```csharp
public record CommerceMsgIntent
{
    // AI-detected intent (đã có IntentDetectionResult)
    public CustomerIntent Intent { get; init; }
    public SubIntentCategory? SubIntent { get; init; }
    public float Confidence { get; init; }

    // Commerce signals — có thể detect bằng keyword hoặc AI
    public bool HasBuySignal { get; init; }      // "chốt đơn", "lấy nhé"
    public bool HasProductQuestion { get; init; } // "nói thêm", "thành phần"
    public bool HasPriceQuestion { get; init; }   // "giá bao nhiêu"
    public bool HasShippingQuestion { get; init; } // "freeship", "phí ship"
    public bool HasInventoryQuestion { get; init; }// "còn hàng"
    public bool HasOrderEstimateQ { get; init; }  // "tổng tiền"
    public bool HasAmbiguousProduct { get; init; } // "cái này", "loại đó"
    public bool IsGenericBuyContinuation { get; init; } // "ok", "ok e", "lấy nha"

    // Structured output theo research recommendation
    public ConsentSignal Consent { get; init; } = ConsentSignal.NotAsked;
    public LeadScore LeadScore { get; init; } = LeadScore.Cold;
    public NextAction NextAction { get; init; } = NextAction.Continue;

    // Composite convenience props
    public bool RequiresProductGrounding =>
        HasProductQuestion || HasPriceQuestion || HasInventoryQuestion;
}

public enum ConsentSignal
{
    NotAsked,          // chưa hỏi consent
    ExplicitGiven,     // khách đồng ý rõ ràng ("đồng ý", "ok lưu cho em")
    Implied,           // khách cung cấp PII chủ động (gửi SĐT khi chưa được hỏi)
    Refused,           // khách từ chối ("không muốn lưu")
    PendingConfirmation
}

public enum LeadScore { Cold, Warm, Hot } // dựa vào HasBuySignal + hasProduct + hasContact + confidence

public enum NextAction
{
    Continue,           // tiếp tục consult
    AskMissingContact,  // hỏi SĐT/địa chỉ
    AskConsent,         // hỏi consent PDPL trước khi lưu
    CreateDraftOrder,
    Escalate            // chuyển human handoff
}
```

### CommerceMsgIntentDetector

```csharp
public interface ICommerceMsgIntentDetector
{
    // Fast path: keyword-based extraction (<1ms)
    CommerceMsgIntent DetectFromKeywords(
        string message, ConversationState state, bool hasProduct, bool hasContact);

    // Full path: keyword + AI intent merge
    Task<CommerceMsgIntent> DetectAsync(
        string message, ConversationState state, bool hasProduct, bool hasContact,
        IReadOnlyList<ConversationMessage> recentHistory, CancellationToken ct = default);
}
```

**Strategy**:
- Commerce signals (buy/product/price/shipping/inventory) → keyword detection (ms)
- `Intent` + `SubIntent` + `Confidence` → AI detection (hiện tại, giữ nguyên)
- Merge vào `CommerceMsgIntent`

### Refactor HandleSalesConversationAsync

Thay ~15 bool declarations bằng:

```csharp
// BEFORE (400+ lines with scattered boolean flags)
var isProductQuestion = ContainsAnyPhrase(message, "nói thêm", ...);
var isShippingQuestion = ContainsAnyPhrase(message, "freeship", ...);
// ...

// AFTER
var msgIntent = await _intentDetector.DetectAsync(message, ctx.CurrentState, ...);

// Branching sử dụng struct thay vì bool soup:
if (msgIntent.HasProductQuestion || (msgIntent.Intent == Questioning && !msgIntent.HasPriceQuestion))
    return await _consultationReplies.BuildProductConsultationReplyAsync(...);

if (msgIntent.HasShippingQuestion)
    return await _consultationReplies.BuildShippingConsultationReplyAsync(...);
```

---

## Migration Strategy

**Không xóa `SalesMessageParser` ngay** — giữ làm implementation detail trong `CommerceMsgIntentDetector`. Refactor từng bước:

1. Tạo `CommerceMsgIntent` record
2. Tạo `ICommerceMsgIntentDetector` với implementation wrap lại `SalesMessageParser` + `IGeminiService.DetectIntentAsync` + `ISubIntentClassifier`
3. Inject vào `SalesStateHandlerBase`
4. Thay `bool soup` trong `HandleSalesConversationAsync` bằng `CommerceMsgIntent`
5. Khi tests pass → cleanup `SalesMessageParser` redundant methods

---

## Files cần tạo

- `Services/Sales/Intent/CommerceMsgIntent.cs` — struct kết quả
- `Services/Sales/Intent/ICommerceMsgIntentDetector.cs`
- `Services/Sales/Intent/CommerceMsgIntentDetector.cs` — implementation

## Files cần sửa

- `StateMachine/Handlers/SalesStateHandlerBase.cs` — inject `ICommerceMsgIntentDetector`, replace boolean flags
- `Configuration/ServiceRegistration/SalesPipelineRegistration.cs` — đăng ký detector

## Files không sửa ngay (cleanup sau)

- `Services/Sales/SalesMessageParser.cs` — giữ nguyên, dùng bên trong detector
- `Services/SubIntent/*` — giữ nguyên, gọi từ detector

---

## Implementation Steps

### Step 1: Tạo CommerceMsgIntent + Detector (1 ngày)

Detector.DetectFromKeywords() wrap SalesMessageParser methods:
```csharp
public CommerceMsgIntent DetectFromKeywords(string message, ...)
{
    return new CommerceMsgIntent
    {
        HasBuySignal = SalesMessageParser.ContainsAnyPhrase(message, "lên đơn", ...),
        HasProductQuestion = SalesMessageParser.ContainsAnyPhrase(message, "nói thêm", ...),
        HasPriceQuestion = SalesMessageParser.ContainsAnyPhrase(message, "giá bao nhiêu", ...),
        // ...
        // Intent/SubIntent/Confidence set mặc định, chờ AI
    };
}
```

Detector.DetectAsync() gọi AI sau đó merge:
```csharp
var keywords = DetectFromKeywords(message, state, hasProduct, hasContact);
var aiIntent = await _gemini.DetectIntentAsync(message, state, hasProduct, hasContact, history, ct);
var subIntent = (aiIntent.Confidence >= threshold && aiIntent.Intent == Consulting)
    ? await _subIntentClassifier.ClassifyAsync(message)
    : null;

return keywords with
{
    Intent = aiIntent.Intent,
    SubIntent = subIntent?.Category,
    Confidence = aiIntent.Confidence
};
```

### Step 2: Inject vào SalesStateHandlerBase (0.5 ngày)

Thêm `ICommerceMsgIntentDetector` vào constructor (sau Phase 01 đã gọn).
Thêm vào `SalesPipelineRegistration`.

### Step 3: Refactor HandleSalesConversationAsync (1 ngày)

Thay thế 15 bool declarations bằng `CommerceMsgIntent`. Giữ nguyên branching logic, chỉ đổi data source.

Sau khi xong: verify tất cả branches vẫn được cover (grep tất cả bool cũ, map sang CommerceMsgIntent property).

### Step 4: Tests (0.5 ngày)

- Unit test `CommerceMsgIntentDetector` với các keyword patterns
- Golden conversation tests (R-01 suite) phải pass 100%
- Không có regression trong branching behavior

---

## Todo

- [ ] Tạo CommerceMsgIntent record
- [ ] Tạo ICommerceMsgIntentDetector
- [ ] Implement CommerceMsgIntentDetector (keyword + AI merge)
- [ ] Thêm vào SalesPipelineRegistration
- [ ] Inject vào SalesStateHandlerBase constructor
- [ ] Replace 15 bool flags với CommerceMsgIntent trong HandleSalesConversationAsync
- [ ] Verify mọi branch vẫn cover
- [ ] Chạy golden tests
- [ ] Build + tests pass

---

## Success Criteria

- `HandleSalesConversationAsync` không còn `ContainsAnyPhrase` trực tiếp (đã move vào Detector)
- Line count HandleSalesConversationAsync giảm ≥ 20%
- Golden test suite (R-01) pass 100%
- `CommerceMsgIntentDetector` có unit tests riêng

---

## Risk

- **Semantic mismatch**: Keyword mapping vào CommerceMsgIntent.HasXxx phải chính xác 1-to-1 với bool cũ → test golden cases kỹ
- **SubIntent + AI intent race**: Hiện tại SubIntentClassifier chạy sau AI intent check. Giữ nguyên thứ tự trong Detector.DetectAsync()
- **A/B test recommendation**: Sau khi ship, monitor `consultationRejectionCount` và `draftOrderCreated` rate 48h để phát hiện regression
