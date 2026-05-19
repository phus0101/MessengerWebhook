---
title: "Live AI/RAG Messenger Transcript Test"
description: "Add one env opt-in integration test validating the MN sales transcript against real AI/RAG services."
status: completed
priority: P2
effort: 3h
branch: feat/subintent-classification-system
tags: [testing, integration, ai, rag, messenger, sales-flow]
created: 2026-05-06
blockedBy: []
blocks: []
---

# Live AI/RAG Messenger Transcript Test

## Overview

Create a narrow, env-gated live integration test for the transcript that previously fell out of checkout after `vẫn dùng thông tin cũ`. The test must use real AI/RAG service paths where practical and must not broaden the production fix scope.

## Scope

- Add one live transcript validation test for the MN flow.
- Gate execution with `RUN_LIVE_AI_RAG_TESTS=true`.
- Use real Gemini/RAG APIs/services, not fake AI/RAG responses.
- Keep existing unit/golden tests unchanged unless required for compile.
- Do not create draft order before final summary confirmation unless existing contract explicitly changes.

## Decisions Locked

- RAG check: run as a preflight inside the same live transcript test.
- Env gate: runtime return when `RUN_LIVE_AI_RAG_TESTS` is not `true`; do not add dynamic-skip package.
- Database: keep the integration-test in-memory database; only AI/RAG dependencies are live.
- RAG data: require MN/product data to be indexed already in the live Pinecone/RAG store; do not index or mutate Pinecone from the test.
- Assertions: verify stable invariants only, not exact AI wording.
- Failures: when env gate is enabled, missing config/API keys or empty RAG result must fail clearly.

## Transcript Under Test

1. `tôi đang tìm sản phẩm mặt nạ dưỡng ẩm`
2. `1`
3. `tư vấn thêm về công dụng và cách dùng`
4. `nói kỹ hơn`
5. `lên đơn cho tôi`
6. `vẫn dùng thông tin cũ`

## Phases

| Phase | Status | File | Goal |
| --- | --- | --- | --- |
| 01 | completed | [phase-01-add-env-gated-live-transcript-test.md](phase-01-add-env-gated-live-transcript-test.md) | Added opt-in transcript test and assertions. |
| 02 | completed | [phase-02-use-minimal-live-service-path.md](phase-02-use-minimal-live-service-path.md) | Added minimal real AI/RAG service path without expanding default test framework. |
| 03 | completed | [phase-03-validate-command-and-docs.md](phase-03-validate-command-and-docs.md) | Validated build, env-off/env-on live command, regression suites, docs, tester, and code review. |

## Key Dependencies

- Existing DI registrations in `src/MessengerWebhook/Program.cs`.
- Existing transcript patterns in `tests/MessengerWebhook.IntegrationTests/StateMachine/TranscriptGoldenFlowTests.cs`.
- Existing RAG test endpoint `POST /api/admin/test-rag` in `src/MessengerWebhook/Endpoints/TestRagEndpointExtensions.cs`.
- Valid local secrets/config for Gemini, Vertex/Pinecone if the selected RAG path requires them.

## Success Criteria

- Default `dotnet test` does not call paid/external AI/RAG APIs.
- Live test runs only when `RUN_LIVE_AI_RAG_TESTS=true`.
- Final transcript turn returns checkout/final summary content, not catalog fallback.
- Product context remains MN.
- Remembered phone/address appear in the confirmation flow.
- RAG service/API path is exercised with real external dependencies before transcript assertion or as a live preflight.

## Completion Evidence

- Implemented `LiveAiRagWebApplicationFactory` in `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`.
- Added `tests/MessengerWebhook.IntegrationTests/StateMachine/LiveAiRagTranscriptIntegrationTests.cs`.
- Updated `docs/project-changelog.md`.
- `dotnet build` passed.
- Env-off filtered integration test passed.
- Tester agent: DONE.
- Code reviewer re-review: DONE, no medium+ issues.
- Env-on live AI/RAG run passed with local credentials/indexed MN data.
- Response validation regression fixed for grounded `Mat Na Ngu` detail replies with conversational suffixes.
- Keyword search now normalizes Vietnamese/ASCII matching and fails closed when tenant context is unresolved.
- Hybrid search now applies explicit `tenant_id` filters to both vector and keyword branches.
- Full unit suite passed: 659/659.
- Full integration suite passed on final run: 235 passed, 7 skipped.
- Tester agent: DONE after independent validation.
- Code reviewer re-review: DONE, no remaining critical/high issues.
