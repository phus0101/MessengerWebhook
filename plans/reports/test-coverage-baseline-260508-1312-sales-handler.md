# Test Coverage Baseline — SalesStateHandlerBase + Sales Handlers

**Date**: 2026-05-08 13:12
**Purpose**: Baseline trước khi bắt đầu Phase R-01 (golden test suite)
**Test command**: `dotnet test --collect:"XPlat Code Coverage"`

## Test execution summary

- **Unit tests**: 666 passed, 0 failed (25s)
- **Integration tests**: 235 passed, 0 failed, 7 skipped (Gemini real API) (1m 59s)
- **Build**: Success, 30+ warnings (CS8625 nullable, CS1998 async without await)

## Overall project coverage (unit tests)

| Metric | Value |
|--------|-------|
| Line coverage | **77.74%** |
| Branch coverage | **49.98%** |
| Lines covered | 30,902 / 39,747 |
| Branches covered | 2,610 / 5,222 |
| Total cyclomatic complexity | 6,924 |

Branch coverage 50% = nhiều code path chưa test, đặc biệt là conditional logic.

## SalesStateHandlerBase coverage

| Metric | Value | Đánh giá |
|--------|-------|----------|
| Line coverage | **74.19%** | OK nhưng không cao |
| Branch coverage | **64.22%** | **THẤP — 35% branch không test** |
| Cyclomatic complexity | **445** | **CỰC CAO** (>10 đã coi là phức tạp) |
| File size | 2,425 dòng | Vượt limit 200 dòng x12 |

**Diễn giải**:
- 74% line coverage = test "chạm" 3/4 dòng code
- 64% branch coverage = nhưng nhiều if/else chỉ test 1 chiều
- Complexity 445 trong 1 class = ~445 path execution có thể, test cover ~64% = nhiều edge case không test

## Sales-related handlers coverage

| Handler | Line | Branch | Complexity | Note |
|---------|------|--------|------------|------|
| **SalesStateHandlerBase** | 74.19% | 64.22% | **445** | Base class, hot path |
| **CompleteStateHandler** | **51.92%** | **0%** | 92 | ⚠️ Close order, coverage thấp |
| **BaseStateHandler** | 31.81% | 0% | 5 | Abstract base, dễ ignore |
| ConsultingStateHandler | (không thấy class entry — kế thừa) | - | - | Kế thừa SalesStateHandlerBase |
| CollectingInfoStateHandler | 100% | 100% | 3 | Wrapper trivial |
| AddToCartStateHandler | 100% | 100% | 2 | Trivial |
| BrowsingProductsStateHandler | 100% | 100% | 2 | Trivial |
| CartReviewStateHandler | 100% | 100% | 2 | Trivial |

## Key risks cho R-01 refactor

### 🔴 RỦI RO CAO — CompleteStateHandler

- **Line 51.92%, branch 0%**, complexity 92
- File 270 dòng (theo earlier check)
- **Branch 0% nghĩa là không có if/else nào được test cả hai chiều**
- Đây là handler **đóng đơn hàng** — bug ở đây = mất tiền khách hàng
- **Hành động**: R-01 phải cover CompleteStateHandler ít nhất 70% branch trước khi refactor

### 🟡 RỦI RO TRUNG BÌNH — SalesStateHandlerBase branch 64%

- 35% branch không test = ~155 path execution không kiểm tra (445 × 35%)
- Refactor 2425 dòng với 35% branch không kiểm chứng = **rất nhiều khả năng break silent**
- **Hành động**: R-01 golden test phải tăng branch coverage SalesStateHandlerBase lên ≥ 85%

### 🟢 OK — concrete handler kế thừa

- AddToCart, Browsing, Cart, Collecting đều 100%
- Các handler này thin, ít risk khi refactor base class

## Implications cho R-01 plan

Plan ban đầu R-01 = 2 ngày. Với data này:

- **Effort R-01 cần tăng từ 2 ngày → 3-4 ngày** vì:
  - Phải capture conversation cover branch không test (CompleteStateHandler đặc biệt)
  - Phải viết unit test bổ sung cho branch hiện tại 0% trước khi refactor
- **Coverage target R-01**:
  - SalesStateHandlerBase: 64% branch → ≥ 85% branch
  - CompleteStateHandler: 0% branch → ≥ 70% branch
  - BaseStateHandler: 0% branch → ≥ 60% branch

## Warnings cần xử lý (không block)

1. **CS8625 (~20 chỗ)**: nullable reference type violations — chủ yếu trong `CompleteStateHandler` (lines 105-118) và `SalesStateHandlerBase:2315`
2. **CS1998 (~6 chỗ)**: async method không có `await` — handler trivial, cần check thực sự là sync hay miss `await`
3. **CS8602 (DraftOrderCoordinator:57)**: possible null dereference — fix khi vào R-03

## Unresolved questions

1. **Có nên fix CS8625/CS1998 trước R-01 không?** — đề xuất fix khi vào R-05 cleanup
2. **Branch 0% của CompleteStateHandler là do test không cover hay do lambda/closure không trace được?** — cần debug coverage tool
3. **Integration test có thực sự test SalesStateHandlerBase không?** — coverage integration thấp hơn unit (61% vs 74%) → integration không boost branch coverage đáng kể
4. **Có thể nâng coverage trước R-01 (như sub-task 0)?** — đề xuất có, viết test cho branch null hiện tại trước khi capture golden
