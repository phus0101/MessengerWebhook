# Phase 01: Complete R-05 — DI Fix Thực Sự

**Priority**: P0  
**Effort**: 1-2 ngày  
**Status**: Complete  
**Depends on**: None (prerequisite cho tất cả phases còn lại)

---

## Vấn đề

R-05 đã đánh dấu Complete trong plan.md nhưng code vẫn còn 2 vấn đề nghiêm trọng:

### Issue 1: Self-instantiation bypass DI

```csharp
// SalesStateHandlerBase.cs:148-171
_contextResolver = contextResolver ?? new SalesContextResolver(
    customerIntelligenceService, productMappingService, ...);
_promptBuilder = promptBuilder ?? new SalesPromptBuilder();
_contactFlow = contactFlow ?? new ContactConfirmationFlow(...);
_replyOrchestrator = replyOrchestrator ?? new SalesReplyOrchestrator(...);
_consultationReplies = consultationReplies ?? new SalesConsultationReplies(...);
```

`SalesPipelineRegistration.cs` đã đăng ký đúng (`AddScoped<ISalesContextResolver, SalesContextResolver>()`) nhưng concrete handlers dùng constructor đầu (không có ISales* params) → DI không inject được → self-instantiation chạy → `NullLogger` hardcoded, không testable.

### Issue 2: Constructor overload bypass PolicyGuardOptions config

```csharp
// Constructor #1 forwards to Constructor #2 với:
Options.Create(new PolicyGuardOptions())  // ← new object mới, bypass IOptions DI config
```

`PolicyGuardOptions` được configure qua `appsettings.json` nhưng bị bỏ qua khi concrete handlers dùng constructor #1.

---

## Mục tiêu

1. Concrete handlers inject ISales* qua DI (không self-instantiate trong base)
2. Base class constructor dùng `IOptions<PolicyGuardOptions>` từ DI
3. Constructor #1 (không PolicyGuardOptions, không ISales*) bị xóa
4. Base class ≤ 800 dòng (hiện 1198, goal R-05 là ≤ 400 nhưng thực tế sau R-04 là 803 lines của logic thật)

---

## Phân tích concrete handlers

Cần kiểm tra các handlers dưới đây — mỗi handler cần thêm ISales* params:

```
StateMachine/Handlers/ConsultingStateHandler.cs
StateMachine/Handlers/CollectingInfoStateHandler.cs
StateMachine/Handlers/DraftOrderStateHandler.cs
StateMachine/Handlers/CompleteStateHandler.cs
StateMachine/Handlers/QuickReplySalesStateHandler.cs
StateMachine/Handlers/HumanHandoffStateHandler.cs
```

---

## Files cần sửa

- `StateMachine/Handlers/SalesStateHandlerBase.cs` — xóa constructor #1, bỏ `?? new Service()`, bỏ optional params
- `StateMachine/Handlers/ConsultingStateHandler.cs` — thêm ISales* params vào constructor
- `StateMachine/Handlers/CollectingInfoStateHandler.cs` — thêm ISales* params
- `StateMachine/Handlers/DraftOrderStateHandler.cs` — thêm ISales* params
- `StateMachine/Handlers/CompleteStateHandler.cs` — thêm ISales* params
- `StateMachine/Handlers/QuickReplySalesStateHandler.cs` — thêm ISales* params
- `StateMachine/Handlers/HumanHandoffStateHandler.cs` — thêm ISales* params (nếu kế thừa SalesStateHandlerBase)
- `plans/260508-1039-production-stabilization/phase-r05-slim-base-class.md` — update status → Complete

---

## Implementation Steps

### Step 1: Audit concrete handlers (0.5 ngày)

Grep tất cả class kế thừa `SalesStateHandlerBase`:
```bash
grep -rn "SalesStateHandlerBase" src/ --include="*.cs"
```

Với mỗi handler:
- Đọc constructor hiện tại
- Xác định params nào cần thêm (ISalesContextResolver, ISalesPromptBuilder, IContactConfirmationFlow, ISalesReplyOrchestrator, ISalesConsultationReplies)
- Xác định params nào đang pass cho `base(...)` call hiện tại

### Step 2: Refactor SalesStateHandlerBase constructor (0.5 ngày)

**Xóa constructor #1** (lines 59-105).

**Sửa constructor #2** (lines 107-172):
- Bỏ optional params (`= null`) cho ISales* — tất cả bắt buộc
- Bỏ `?? new Service(...)` fallback
- Bỏ `Options.Create(new PolicyGuardOptions())` logic

```csharp
// BEFORE (constructor #2 với optional ISales*)
protected SalesStateHandlerBase(
    ...,
    IOptions<PolicyGuardOptions> policyGuardOptions,
    ...,
    ISalesContextResolver? contextResolver = null,  // ← xóa optional
    ...)
{
    _contextResolver = contextResolver ?? new SalesContextResolver(...);  // ← xóa
}

// AFTER (single constructor, all required)
protected SalesStateHandlerBase(
    ...,
    IOptions<PolicyGuardOptions> policyGuardOptions,
    ...,
    ISalesContextResolver contextResolver,   // ← required
    ISalesPromptBuilder promptBuilder,
    IContactConfirmationFlow contactFlow,
    ISalesReplyOrchestrator replyOrchestrator,
    ISalesConsultationReplies consultationReplies)
{
    _contextResolver = contextResolver;  // ← direct assign
}
```

### Step 3: Update concrete handlers (0.5 ngày)

Mỗi handler thêm ISales* vào constructor và pass vào `base(...)`:

```csharp
public ConsultingStateHandler(
    IGeminiService geminiService,
    ...,
    ISalesContextResolver contextResolver,       // thêm
    ISalesPromptBuilder promptBuilder,           // thêm
    IContactConfirmationFlow contactFlow,        // thêm
    ISalesReplyOrchestrator replyOrchestrator,   // thêm
    ISalesConsultationReplies consultationReplies) // thêm
    : base(geminiService, ..., contextResolver, promptBuilder, contactFlow, replyOrchestrator, consultationReplies)
{
}
```

DI sẽ tự inject vì `SalesPipelineRegistration.cs` đã có:
```csharp
services.AddScoped<ISalesContextResolver, SalesContextResolver>();
services.AddSingleton<ISalesPromptBuilder, SalesPromptBuilder>();
// ...
```

### Step 4: Chạy build + golden tests (0.25 ngày)

```bash
dotnet build
dotnet test tests/MessengerWebhook.UnitTests
dotnet test tests/MessengerWebhook.IntegrationTests
```

Tất cả phải pass trước khi commit.

---

## Todo

- [ ] Grep tất cả handlers kế thừa SalesStateHandlerBase
- [ ] Audit từng handler constructor
- [ ] Xóa constructor #1 khỏi SalesStateHandlerBase
- [ ] Bỏ optional params và `?? new` trong constructor #2
- [ ] Cập nhật ConsultingStateHandler
- [ ] Cập nhật CollectingInfoStateHandler
- [ ] Cập nhật DraftOrderStateHandler
- [ ] Cập nhật CompleteStateHandler
- [ ] Cập nhật QuickReplySalesStateHandler
- [ ] Cập nhật HumanHandoffStateHandler (nếu applicable)
- [ ] Build pass
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Update phase-r05 status → Complete

---

## Success Criteria

- `dotnet build` 0 error, 0 warning liên quan DI
- Không còn `?? new` trong `SalesStateHandlerBase.cs`
- Không còn `Options.Create(new PolicyGuardOptions())` trong base class
- Constructor count = 1 trong SalesStateHandlerBase
- Tất cả existing tests pass

---

## Risk

- **Handlers có constructor đặc biệt**: Một số handler có thể inject additional services mà base class không cần — không sao, chỉ thêm ISales* vào cuối param list
- **Unit tests cho handlers**: Nếu test mock constructor cũ, cần update test setup — DI registration đúng thì test sẽ dùng interface mock tốt hơn

---

## Notes

R-05 phase file đánh dấu "Pending" nhưng plan.md đánh dấu "Complete" — inconsistency này cần fix sau khi phase này done.

`NullLogger<SalesContextResolver>.Instance` trong self-instantiation sẽ biến mất → logging đầy đủ hơn trong production.
