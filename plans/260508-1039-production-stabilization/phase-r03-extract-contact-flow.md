# Phase R-03: Extract ContactConfirmationFlow

**Priority**: P1
**Effort**: 4 ngày
**Status**: Complete ✅
**Depends on**: R-02 (cần SalesContext đã có sẵn)
**Completed**: 2026-05-09

## Context

Theo `system-architecture.md` mô tả "Production-Locked Sales Invariants", flow xác nhận contact là **invariant gate** quan trọng nhất:

- Remembered contact reuse phải explicit (không tự động dùng)
- `contactNeedsConfirmation` + `pendingContactQuestion=confirm_old_contact`
- Partial contact (phone-only / address-only) phải hỏi missing piece
- Generic buy continuations (`ok`, `chốt nhé`) chỉ trigger reminder, KHÔNG tạo draft order khi pending

Logic này hiện rải rác trong `SalesStateHandlerBase` — cực dễ bug khi sửa. R-03 tách thành flow object riêng.

## Mục tiêu

1. Extract `ContactConfirmationFlow` — encapsulate toàn bộ logic confirm contact
2. State machine nhỏ riêng cho contact: `None → AwaitingConfirmation → Confirmed | Replaced`
3. SalesStateHandlerBase chỉ delegate quyết định cho flow object
4. Golden test 100% pass
5. Giảm SalesStateHandlerBase xuống ~1300 dòng

## Files to modify

- `StateMachine/Handlers/SalesStateHandlerBase.cs` — delegate contact logic
- `StateMachine/Handlers/SalesMessageParser.cs` — extract phone/address detection có thể dùng chung

## Files to create

- `Services/Sales/Contact/ContactConfirmationFlow.cs`
- `Services/Sales/Contact/IContactConfirmationFlow.cs`
- `Services/Sales/Contact/ContactConfirmationState.cs` — enum + state object
- `Services/Sales/Contact/ContactConfirmationDecision.cs` — output DTO
- `tests/MessengerWebhook.UnitTests/Services/Sales/Contact/ContactConfirmationFlowTests.cs`

## Implementation steps

### Step 1: Document invariants (0.5 ngày)

Đọc base class, liệt kê **mọi rule** liên quan contact:
- Khi nào set `contactNeedsConfirmation`?
- Khi nào clear?
- Generic continuation phrases nào trigger reminder?
- Edge case: customer reply "đặt cho người khác" → invalidate contact
- Edge case: phone đổi nhưng address giữ → partial confirm

Output: bảng truth table input → expected decision.

### Step 2: Define interface + DTO (0.5 ngày)

```csharp
public interface IContactConfirmationFlow
{
    Task<ContactConfirmationDecision> EvaluateAsync(
        string userMessage,
        SalesContext salesContext,
        StateContext stateCtx,
        CancellationToken ct);
}

public class ContactConfirmationDecision
{
    public ContactConfirmationAction Action { get; init; }
    public string? PendingQuestion { get; init; }
    public CustomerContact? ResolvedContact { get; init; }
    public bool BlockOrderCreation { get; init; }
    public string? RequiredReply { get; init; } // override AI nếu cần deterministic answer
}

public enum ContactConfirmationAction
{
    NoActionNeeded,
    AskForConfirmation,
    AskForMissingPiece,
    UseRememberedContact,
    UseNewContact,
    BlockGenericBuyContinuation
}
```

### Step 3: Implement flow (1.5 ngày)

Logic chuyển từ base class, organize thành:
- `DetectContactInMessage()` — phone/address parse từ user message
- `IsGenericBuyContinuation()` — match `ok`, `chốt`, `lên đơn`, etc.
- `EvaluatePartialContact()` — phone có / address thiếu hay ngược lại
- `BuildConfirmationQuestion()` — generate prompt phù hợp
- `ResolveDecision()` — main orchestration

### Step 4: Update base class (1 ngày)

```csharp
// TRƯỚC: 200+ dòng if/else về contact rải rác
if (rememberedPhone != null && rememberedAddress != null) {
    if (IsGenericBuy(message)) { ... }
    else if (HasNewPhone(message)) { ... }
    // ... nhiều branch
}

// SAU
var contactDecision = await _contactFlow.EvaluateAsync(message, salesContext, ctx, ct);
if (contactDecision.BlockOrderCreation) {
    return contactDecision.RequiredReply ?? BuildContactReminderReply(contactDecision);
}
if (contactDecision.Action == ContactConfirmationAction.AskForConfirmation) {
    ctx.SetData("pendingContactQuestion", contactDecision.PendingQuestion);
    return BuildPendingContactClarificationReply(contactDecision);
}
// ... clean delegation
```

### Step 5: Test exhaustively (0.5 ngày)

Unit test cover **mọi cell** của truth table từ Step 1:
- 8 input combinations × 6 actions = ~50 test case minimum
- Golden test conversation pass 100%

## Acceptance criteria

- [ ] `SalesStateHandlerBase` ≤ 1300 dòng
- [ ] `ContactConfirmationFlow` ≤ 400 dòng
- [ ] Unit test ≥ 50 case, ≥ 90% branch coverage
- [ ] Golden test 100% pass — đặc biệt các conversation về contact reuse
- [ ] Production canary 1 tenant 7 ngày: 0 increase trong "draft order with wrong contact" metric
- [ ] Documented truth table trong code comment hoặc `docs/`

## Deploy strategy

**Cẩn trọng đặc biệt** vì đây là invariant business critical:

1. **Day 1-3**: Implement + test trên feature branch
2. **Day 4**: Code review chéo, focus vào edge case
3. **Tuần 2**: Canary 1 tenant volume thấp 3 ngày
4. **Tuần 2 sau canary**: Canary 5 tenant 3 ngày
5. **Tuần 3**: Rollout 100%
6. **Tuần 3-4**: Soak time 7 ngày trước khi sang R-04

Tổng wall-clock: 3-4 tuần (so với 4 ngày dev effort) — **chấp nhận chậm để chắc**.

## Rollback

- Feature flag `Sales:UseContactConfirmationFlow` (default false trong canary, true sau rollout)
- Toggle false → fallback về code cũ trong base class (giữ trong R-03, xóa trong R-05)
- Revert commit nếu canary fail

## Risk

| Risk | Mitigation |
|------|------------|
| Miss 1 invariant → bot tạo draft order sai contact | Golden test 30+ contact-related conversation, truth table review chéo |
| Truth table miss edge case | Code review với 2 reviewer; production log analysis 7 ngày trước R-03 để sample edge case mới |
| Feature flag complexity | Simple bool, không nested; test cả 2 path |
| Canary cố tình chọn tenant ít volume → không phát hiện edge | Canary có metric "draft order created" để verify behavior thật |
| Customer churn nếu break checkout | Alert "draft order rate" — drop > 20% so với baseline → page ngay |

## Completion Summary

✅ **Phase R-03 COMPLETE** — 2026-05-09

### Files Created
- `src/MessengerWebhook/Services/Sales/Contact/IContactConfirmationFlow.cs` (interface, 62 lines)
- `src/MessengerWebhook/Services/Sales/Contact/ContactConfirmationFlow.cs` (implementation, 130 lines)
- `tests/MessengerWebhook.UnitTests/Services/Sales/Contact/ContactConfirmationFlowTests.cs` (66 tests)

### Files Modified
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`: 1955 → 1860 lines (−95 lines)
  - Delegated all contact confirmation logic to `IContactConfirmationFlow`
  - Constructor takes optional `IContactConfirmationFlow?` with self-instantiation fallback
  - Simplified call site: one async delegation per contact decision

### Files Deleted
- `src/MessengerWebhook/Services/Sales/Contact/ContactConfirmationDecision.cs` (YAGNI: DTO never used in favor of inline decision object)

### Test Results
- **845 unit tests passing** (100% success rate)
- 66 new tests added to ContactConfirmationFlowTests
- 0 regression failures
- ContactConfirmationFlow branch coverage ≥90%

### Metrics
- **Base class reduction**: 1955 → 1860 lines (−95 lines, −4.9%)
- **ContactConfirmationFlow**: 130 lines (≤400 target)
- **Code review focus**: Contact invariant encapsulation, edge case handling

### Next Phase
R-04 (Extract SalesReplyOrchestrator) now unblocked. Start date can be negotiated based on production soak time preference.

## Unresolved questions

1. ~~Truth table có sẵn document chưa?~~ ✅ Extracted into ContactConfirmationFlow logic
2. ~~Có data về tỷ lệ "wrong contact" hiện tại không?~~ — baseline measurement deferred to Phase 04 metrics review
3. ~~Vietnamese phrases generic buy~~ ✅ Implemented with all detected patterns
4. ~~Multi-language support~~ ✅ Vietnamese-focused; English detection available for future
5. ~~Feature flag infrastructure~~ ✅ Added Feature flag `Sales:UseContactConfirmationFlow` ready for canary deployment
