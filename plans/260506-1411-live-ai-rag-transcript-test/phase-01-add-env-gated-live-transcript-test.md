# Phase 01 — Add Env-Gated Live Transcript Test

## Context Links

- `tests/MessengerWebhook.IntegrationTests/StateMachine/TranscriptGoldenFlowTests.cs`
- `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`

## Overview

Priority: high  
Status: completed  
Add one opt-in live integration test that replays the exact MN transcript and verifies the final turn stays in checkout.

## Key Insights

- Existing golden transcript tests show the correct turn-by-turn `IStateMachine.ProcessMessageAsync(...)` pattern.
- Existing `CustomWebApplicationFactory` replaces `IGeminiService`, `IEmbeddingService`, `IHybridSearchService`, and related dependencies with test doubles.
- This phase should define the transcript test contract first; Phase 02 decides the smallest way to use real AI/RAG paths.

## Requirements

### Functional

- Add a new live test class, recommended path: `tests/MessengerWebhook.IntegrationTests/StateMachine/LiveAiRagTranscriptIntegrationTests.cs`.
- Return early at runtime unless `RUN_LIVE_AI_RAG_TESTS=true`; do not add a dynamic-skip package.
- Seed returning customer phone/address in the in-memory test database before transcript replay.
- Run the live RAG preflight in the same test before transcript replay.
- Replay these turns:
  1. `tôi đang tìm sản phẩm mặt nạ dưỡng ẩm`
  2. `1`
  3. `tư vấn thêm về công dụng và cách dùng`
  4. `nói kỹ hơn`
  5. `lên đơn cho tôi`
  6. `vẫn dùng thông tin cũ`
- Assert final response contains checkout/final summary language.
- Assert final response contains MN/product identity and remembered phone/address.
- Assert final response does not contain RAG/catalog fallback like `Không tìm thấy sản phẩm phù hợp` or `chưa tìm thấy dữ liệu sản phẩm phù hợp`.

### Non-Functional

- No external API calls unless env gate is enabled.
- Keep test deterministic enough for pre-restart validation.
- Avoid broad fixture rewrites.

## Architecture

```text
LiveAiRagTranscriptIntegrationTests
  -> if RUN_LIVE_AI_RAG_TESTS != true: return early
  -> isolated in-memory app/test scope
  -> live RAG preflight for "mặt nạ dưỡng ẩm"
  -> seed returning customer
  -> IStateMachine.ProcessMessageAsync per turn
  -> assert final checkout invariants
```

## Related Code Files

### Modify/Create

- Create `tests/MessengerWebhook.IntegrationTests/StateMachine/LiveAiRagTranscriptIntegrationTests.cs`

### Read Only

- `tests/MessengerWebhook.IntegrationTests/StateMachine/TranscriptGoldenFlowTests.cs`
- `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

## Implementation Steps

1. Add an env helper inside the test class or reuse an existing test utility if present.
2. If env var is absent, return early; do not add package-level dynamic skip.
3. Build the state machine scope using the live-capable setup from Phase 02.
4. Initialize tenant context.
5. Run direct live RAG preflight for `mặt nạ dưỡng ẩm` and fail clearly if MN/product data is not found.
6. Seed returning customer identity with phone `0888129403` and address `4/6/20, ttn1, hcm`.
7. Replay transcript turns in order.
8. Capture final turn response.
9. Assert invariant-only final summary behavior and absence of fallback.
10. Assert no draft order exists before final customer confirmation if repository access is available in the existing fixture.

## Todo List

- [x] Create live transcript test class.
- [x] Add runtime env-gate return.
- [x] Add same-test live RAG preflight.
- [x] Seed returning customer in in-memory DB.
- [x] Replay transcript.
- [x] Add invariant-only final-response assertions.
- [x] Add no-premature-draft assertion if existing repository pattern is simple.

## Success Criteria

- Test is inert by default.
- Without env enabled, test returns early and makes no external API calls.
- With env enabled and valid API config/indexed RAG data, transcript reaches final summary after `vẫn dùng thông tin cũ`.
- Test fails clearly if RAG preflight cannot find indexed MN/product data.
- Test fails if flow falls through to RAG/catalog fallback on the final turn.

## Risk Assessment

- Live AI output can vary. Mitigation: assert stable checkout invariants, not exact full response.
- Real RAG data may drift. Mitigation: assert MN/product identity and fallback absence.
- Existing factory stubs AI/RAG. Mitigation: Phase 02 must bypass or minimally override stubs.

## Security Considerations

- Do not log secrets or API keys.
- Do not commit `.env`.
- Keep live test opt-in to avoid unexpected API use in CI.

## Next Steps

Proceed to Phase 02 to choose the minimal live AI/RAG service path.
