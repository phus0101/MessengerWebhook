# Phase 02 — Use Minimal Live Service Path

## Context Links

- `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`
- `tests/MessengerWebhook.IntegrationTests/Services/GeminiApiIntegrationTests.cs`
- `src/MessengerWebhook/Program.cs`
- `src/MessengerWebhook/Services/RAG/RAGService.cs`
- `src/MessengerWebhook/Endpoints/TestRagEndpointExtensions.cs`

## Overview

Priority: high  
Status: completed  
Wire only the minimum needed to make the live test exercise real AI/RAG service paths.

## Key Insights

- `CustomWebApplicationFactory` currently removes and stubs key external services.
- Real app DI already registers `IGeminiService`, `IEmbeddingService`, `IVectorSearchService`, `IHybridSearchService`, `IContextAssembler`, and `IRAGService`.
- `RAGService` requires tenant context; tests must initialize tenant before calling RAG.
- `POST /api/admin/test-rag` directly calls `IRAGService.RetrieveContextAsync(...)` and can serve as a live RAG preflight if HTTP testing is simpler than service-level invocation.

## Requirements

### Functional

- Prefer a small live fixture or factory option over modifying the default test factory behavior.
- Ensure the transcript test uses real `IGeminiService` for AI-dependent turns.
- Exercise real RAG as an in-test preflight before transcript replay.
- Prefer direct `IRAGService.RetrieveContextAsync(...)` over `POST /api/admin/test-rag` unless HTTP coverage is necessary.
- Keep the integration-test in-memory database.
- Require MN/product data to be indexed already in the live Pinecone/RAG store; do not call indexing/upsert pipelines from this test.
- Keep Messenger outbound service stubbed/spied if needed to avoid sending real Facebook messages.
- Keep email/Nobita side effects stubbed unless they are required for this transcript.

### Non-Functional

- Do not broaden the existing `CustomWebApplicationFactory` default behavior.
- Avoid duplicate full application bootstrapping if a small option can restore real services.
- Keep configuration from existing appsettings + environment variables.

## Architecture

Preferred minimal approach:

```text
CustomWebApplicationFactory(liveAiRag: true)
  keeps in-memory test database
  keeps safe side-effect stubs: Messenger, Email, Nobita
  uses real services: Gemini, Embedding, Vector/Pinecone, HybridSearch, RAG
  initializes tenant context
  runs direct IRAGService preflight query: "mặt nạ dưỡng ẩm"
  requires indexed MN/product data already present in live RAG store
  runs state machine transcript
```

Fallback approach:

```text
Separate live test service provider
  -> load configuration like GeminiApiIntegrationTests
  -> construct real RAG/Gemini dependencies using production DI
  -> use existing test app only for state machine if full live fixture is too invasive
```

## Related Code Files

### Modify/Create

- Modify `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs` only if needed to add a narrow live AI/RAG option.
- Create/modify `tests/MessengerWebhook.IntegrationTests/StateMachine/LiveAiRagTranscriptIntegrationTests.cs`.

### Read Only

- `src/MessengerWebhook/Program.cs`
- `src/MessengerWebhook/Endpoints/TestRagEndpointExtensions.cs`
- `src/MessengerWebhook/Services/RAG/RAGService.cs`
- `tests/MessengerWebhook.IntegrationTests/Services/GeminiApiIntegrationTests.cs`

## Implementation Steps

1. Inspect `CustomWebApplicationFactory` constructors/options before editing.
2. Add a narrow option such as `UseLiveAiRag` only if no simpler fixture exists.
3. Keep the existing in-memory database setup for live test mode.
4. When live mode is enabled, do not remove/replace `IGeminiService`, `IEmbeddingService`, `IHybridSearchService`, or `IRAGService`.
5. Keep services with real external side effects stubbed: Messenger send, email, Nobita/order submission if not required.
6. Initialize tenant context before RAG preflight and transcript replay.
7. Add a direct RAG preflight query for `mặt nạ dưỡng ẩm` through `IRAGService.RetrieveContextAsync(...)`.
8. Assert RAG preflight returns indexed MN/product data; fail clearly if it does not.
9. Do not call `ProductEmbeddingPipeline` or any Pinecone upsert/indexing path.
10. Run transcript test using the same live-capable scope.

## Todo List

- [x] Determine if factory option is necessary.
- [x] Add minimal live AI/RAG factory path if necessary.
- [x] Preserve in-memory database setup.
- [x] Preserve safe stubs for external side effects.
- [x] Initialize tenant before RAG call.
- [x] Add direct `IRAGService` preflight assertion.
- [x] Confirm no indexing/upsert path runs.
- [x] Confirm transcript uses real AI/RAG path, not test doubles.

## Success Criteria

- Default integration tests still use current stubs.
- Live test can use real Gemini/RAG dependencies with env opt-in.
- RAG preflight proves product retrieval path works against already-indexed live data before transcript assertion.
- No Pinecone/indexing mutation is triggered by the test.
- No real Facebook/email/order side effects are triggered.

## Risk Assessment

- Restoring all production services may trigger unwanted outbound calls. Mitigation: keep only AI/RAG real, keep side-effect services stubbed.
- RAG may need Pinecone/Vertex config. Mitigation: fail clearly when env gate is enabled but config is missing.
- Tenant context missing causes empty RAG result. Mitigation: explicitly initialize tenant before RAG preflight.

## Security Considerations

- Never print API keys.
- Keep test failure messages focused on missing config names, not values.
- Do not store live credentials in test files.

## Next Steps

Proceed to Phase 03 to validate commands and document run usage.
