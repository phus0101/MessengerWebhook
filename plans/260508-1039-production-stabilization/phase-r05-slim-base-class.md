# Phase R-05: Slim Base Class + Final Cleanup

**Priority**: P2
**Effort**: 2 ngày
**Status**: Pending
**Depends on**: R-04 (3 service đã extract + soak time)

## Context

Sau R-02..R-04, `SalesStateHandlerBase` còn ~600 dòng nhưng vẫn:
- 20+ dependency trong constructor (di sản)
- Method utility/helper rải rác
- Code dead (đã thay bằng service nhưng chưa xóa)
- Feature flag fallback từ R-03

R-05 dọn dẹp cuối — file gọn, tài liệu cập nhật, feature flag retire.

## Mục tiêu

1. Slim `SalesStateHandlerBase` xuống ≤ 400 dòng
2. Constructor injection còn ≤ 8 dependency (chỉ những gì base class thật sự dùng)
3. Xóa code dead, comment dead
4. Retire feature flags (R-03)
5. Update `docs/system-architecture.md` reflect kiến trúc mới
6. Final golden test pass

## Files to modify

- `StateMachine/Handlers/SalesStateHandlerBase.cs`
- `StateMachine/Handlers/Consulting*, Collecting*, DraftOrder*, Complete*` — concrete handler có thể giảm constructor cũng
- `docs/system-architecture.md` — section "Production-Locked Sales Invariants" cập nhật
- `docs/codebase-summary.md` — reflect thay đổi
- `Program.cs` (hoặc Configuration/SalesPipelineRegistration.cs nếu Phase 05 đã làm)

## Files to delete

- Code path cũ trong base class (đã thay bằng service)
- Feature flag config entries (sau khi retire)

## Implementation steps

### Step 1: Audit dead code (0.5 ngày)

- Grep mọi method private trong SalesStateHandlerBase
- Đánh dấu method nào còn caller (giữ), method nào không (xóa)
- Tools hỗ trợ: ReSharper "Find Usages", hoặc Roslyn analyzer

Cẩn thận: method có thể được gọi qua reflection hoặc concrete handler. Verify bằng grep trước khi xóa.

### Step 2: Reduce constructor (0.5 ngày)

20+ dependency hiện tại — sau R-02..R-04, một số đã không còn dùng trực tiếp ở base class (đã chuyển vào service).

Dependency còn cần ở base class:
- `ILogger`
- `IGeminiService` — có thể chỉ còn cho fallback path? Verify.
- `ISalesContextResolver` (mới)
- `ISalesPromptBuilder` (mới)
- `IContactConfirmationFlow` (mới)
- `ISalesReplyOrchestrator` (mới)
- `DraftOrderCoordinator`
- `IOptions<SalesBotOptions>`

Target: 8 dependency. Mọi dependency khác phải remove khỏi base, có thể vẫn ở concrete handler nếu cần.

### Step 3: Retire feature flag (0.5 ngày)

Từ R-03 có flag `Sales:UseContactConfirmationFlow`. Sau ≥ 4 tuần production xanh:
- Set default true cứng trong code
- Xóa fallback path cũ
- Remove flag khỏi appsettings

### Step 4: Update docs (0.5 ngày)

`docs/system-architecture.md` section sales:
- Update class diagram (text-based hoặc Mermaid)
- Mô tả 4 service mới: SalesContextResolver, SalesPromptBuilder, ContactConfirmationFlow, SalesReplyOrchestrator
- Pipeline diagram cho ReplyOrchestrator stages

`docs/codebase-summary.md`:
- Update LOC count
- Thay note "SalesStateHandlerBase 2425 lines" → "SalesStateHandlerBase ~400 lines + 4 services"

## Acceptance criteria

- [ ] `SalesStateHandlerBase` ≤ 400 dòng
- [ ] Constructor ≤ 8 dependency
- [ ] 0 method private không có caller (verified)
- [ ] 0 feature flag từ R-03 còn trong code
- [ ] Docs updated, có Mermaid diagram class
- [ ] Golden test 100% pass
- [ ] Full test suite pass
- [ ] Production stable 7 ngày sau deploy

## Deploy strategy

R-05 ít rủi ro nhất (chủ yếu xóa dead code) nhưng vẫn:
1. Day 1-2: Implement
2. Day 3: Code review chéo
3. Tuần 9 day 4-5: Canary + rollout
4. Tuần 9 cuối tuần: Soak

## Rollback

- Pure cleanup, revert commit nếu fail
- Đặc biệt cẩn thận với "xóa dead code" — nếu method bị reflection gọi, build pass nhưng runtime fail

## Risk

| Risk | Mitigation |
|------|------------|
| Xóa method được dùng qua reflection | Search string literal của method name trước khi xóa |
| Concrete handler vẫn dùng dependency đã remove | Compile error sẽ catch; lint full solution |
| Docs out of date sau khi merge feature khác | Schedule docs review monthly |
| Feature flag retire sớm gây bug → khó rollback | Đợi đủ 4 tuần production xanh |

## Final acceptance — toàn bộ refactor

Sau R-05, verify toàn bộ refactor đã đạt mục tiêu:

- [ ] **Code metric**:
  - SalesStateHandlerBase: 2425 → ≤ 400 dòng (84% reduction)
  - 4 new service files, mỗi file < 400 dòng
  - Total LOC sales pipeline: tương đương hoặc giảm (không tăng do over-engineer)

- [ ] **Quality metric**:
  - Unit test coverage sales pipeline ≥ 85%
  - Golden test ≥ 100 conversation, 100% pass
  - Branch coverage SalesStateHandlerBase ≥ 90%

- [ ] **Production metric** (so với baseline Phase 02):
  - p50/p95/p99 latency: drift ≤ 10%
  - Error rate: drift ≤ 0.1 percentage point
  - Draft order success rate: drift ≤ 1%
  - 0 net-new bug từ refactor (tracked qua incident log)

- [ ] **Maintainability** (chủ quan, dev assess):
  - Có thể đọc + hiểu base class trong < 30 phút
  - Thêm sub-intent mới chỉ chạm 1-2 file
  - Đổi pipeline order chỉ chạm 1 file (DI registration)

## Unresolved questions

1. **Có cần Mermaid diagram trong docs không?** — đề xuất có, helpful cho onboarding
2. **Codebase summary update cadence** — manual hay automation?
3. **Lessons learned doc** — viết retrospective sau refactor xong? Lưu ở `docs/journals/`?
