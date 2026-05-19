# Phase 01 — Quick fix: similarity threshold + category fallback

**Priority:** P0
**Status:** Not started
**Feature flag:** `RAG:Threshold:Enabled` (default `false`)
**Estimated:** 1-2 days

## Context links
- Plan overview: [plan.md](plan.md)
- Related reports:
  - `plans/reports/analysis-260507-1614-rag-conversation-evaluation.md`
  - `plans/reports/analysis-260507-2132-rag-not-working-after-reindex.md`
- Existing categories taxonomy: `Services/ProductGrounding/ProductGroundingService.cs:26-35` (RelatedTermGroups)

## Overview
Stop the bleeding. Three minimal changes behind a feature flag:
1. **Similarity threshold filter** on Pinecone results.
2. **Skip-RAG path** for short browsing queries (≤ 2 tokens AND intent=Browsing).
3. **Category-listing fallback** when RAG returns empty/filtered.

No new services, no architectural changes. All toggleable via config.

## Key insights
- Pinecone `Score` is already populated (`PineconeVectorService.cs:209`) — just need to filter.
- `RelatedTermGroups` static array already enumerates 7 skincare categories — reuse as the category menu source.
- `IProductNeedDetector` (used by ProductGroundingService) already does keyword matching — extend it (or piggyback) to emit `IsCategoryQuery(message, out string? category)` boolean for routing.

## Requirements

### Functional
- F1: When `RAG:Threshold:Enabled=true`, Pinecone results with `Score < RAG:MinSimilarityScore` are dropped before fusion in `HybridSearchService`.
- F2: When `Intent:SkipRagForShortBrowsing:Enabled=true`, queries with ≤2 tokens classified as Browsing route to a new `CategoryBrowseFallback` handler instead of RAG.
- F3: `CategoryBrowseFallback` returns a Vietnamese reply listing top 5 categories from `RelatedTermGroups`, with a clarifying question.
- F4: Existing fallback in `ProductGroundingService.FallbackReply` is rewritten to also offer categories (not just ask for SKU).

### Non-functional
- Zero regression on 916/916 existing tests.
- Feature flags default OFF in prod `appsettings.json`, ON in `appsettings.Development.json`.
- Latency impact: <5ms (threshold filter is in-memory LINQ; skip-RAG path saves time).

## Architecture

```
Webhook → SalesHandler → DetectIntent (existing)
                            │
                            ├─[NEW] Browsing AND tokens≤2 ─→ CategoryBrowseFallback ─→ reply
                            │
                            └─ default path ─→ HybridSearch
                                                  │
                                                  ├─[NEW] threshold filter (drop score < min)
                                                  │
                                                  └─ ProductGrounding ─→ reply
                                                       │
                                                       └─[CHANGED] FallbackReply lists categories
```

## Related code files

### Modify
- `src/MessengerWebhook/Services/VectorSearch/PineconeVectorService.cs` (~line 199-210): add post-filter on Score.
- `src/MessengerWebhook/Services/VectorSearch/HybridSearchService.cs` (~line 40-100): pass threshold from options into vector + keyword merger.
- `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs` (~line 22, line 76-121): rewrite FallbackReply, add category-listing helper.
- `src/MessengerWebhook/Services/Sales/SalesStateHandlerBase.cs` (~line 306): add short-browsing branch before RAG.
- `src/MessengerWebhook/appsettings.json` (line 143-148 RAG section): add `MinSimilarityScore`, `Threshold:Enabled` keys.
- `src/MessengerWebhook/Services/Configuration/RagOptions.cs` (or equivalent): add new option properties.

### Create
- `src/MessengerWebhook/Services/Sales/CategoryBrowse/CategoryBrowseFallbackHandler.cs` (~80 LOC max — list categories from RelatedTermGroups).
- `src/MessengerWebhook/Services/Sales/CategoryBrowse/ICategoryBrowseFallbackHandler.cs` (interface).
- `tests/MessengerWebhook.UnitTests/Services/Sales/CategoryBrowse/CategoryBrowseFallbackHandlerTests.cs`.
- `tests/MessengerWebhook.UnitTests/Services/VectorSearch/PineconeThresholdFilterTests.cs`.

### Delete
- None.

## Implementation steps

1. **Add config keys** in `appsettings.json` + Development overrides + `RagOptions.cs`:
   ```jsonc
   "RAG": {
     "Enabled": true,
     "TopK": 5,
     "MinSimilarityScore": 0.50,    // NEW — tunable starting point
     "Threshold": { "Enabled": false }, // NEW — feature flag
     "FallbackStrategy": "full-context",
     "TimeoutMs": 5000
   },
   "Intent": {
     "SkipRagForShortBrowsing": { "Enabled": false } // NEW
   }
   ```
   → verify: `dotnet build` clean.

2. **Apply threshold filter** in `PineconeVectorService.cs:199-210`:
   - Inject `IOptionsMonitor<RagOptions>`.
   - After `Select(...)`, add `.Where(r => !threshold.Enabled || r.Score >= threshold.MinSimilarityScore)`.
   - Log dropped count.
   → verify: unit test `PineconeThresholdFilterTests` covers (flag off = no filter, flag on + low score = filtered, flag on + high score = kept).

3. **Rewrite fallback** in `ProductGroundingService.cs:22`:
   - Replace `FallbackReply` string with method `BuildFallbackReply(string? detectedCategory)`.
   - If category detected (via `ExtractRelatedCriteria` line 132-152) → reply with that category's top 3 products from DB.
   - Else → list categories: "Dạ shop em có các dòng skincare gồm: ① Sữa rửa mặt ② Toner ③ Serum ④ Kem chống nắng ⑤ Tẩy trang. Anh/chị quan tâm dòng nào ạ?"
   → verify: existing tests `ProductGroundingServiceTests` updated, new tests cover both branches.

4. **Implement `CategoryBrowseFallbackHandler`**:
   - Inject `IProductRepository`, `IOptionsMonitor<IntentOptions>`.
   - Method `Task<string> HandleAsync(string customerMessage, CancellationToken ct)`.
   - Detects category via reused `IProductNeedDetector` — if match, list 3 bestsellers in that category; else generic category menu.
   → verify: unit tests pass.

5. **Wire branching** in `SalesStateHandlerBase.cs:306`:
   ```csharp
   if (intentOptions.SkipRagForShortBrowsing.Enabled
       && intent == Browsing
       && customerMessage.Split(' ').Length <= 2)
   {
       return await _categoryBrowseFallback.HandleAsync(customerMessage, ct);
   }
   // else continue existing RAG path
   ```
   → verify: integration test reproduces the failing transcript and now returns category menu.

6. **Add regression transcript test**:
   - File: `tests/MessengerWebhook.IntegrationTests/Transcripts/DiscoveryCategoryTranscriptTests.cs`.
   - Cases: "shop có sản phẩm nào về da không", "có gì cho mụn không", "shop bán gì", "cho xem mặt nạ".
   → verify: 4 new cases pass.

7. **Update docs**:
   - `docs/codebase-summary.md`: mention new `CategoryBrowseFallback` module.
   - `docs/system-architecture.md`: update flow diagram.
   - `docs/project-changelog.md`: log Phase 01.
   → verify: docs reviewed by `docs-manager` agent.

## Todo
- [ ] Add config keys + RagOptions/IntentOptions properties
- [ ] Implement threshold filter in PineconeVectorService
- [ ] Rewrite ProductGroundingService.FallbackReply with category branch
- [ ] Create CategoryBrowseFallbackHandler + interface + DI registration
- [ ] Wire skip-RAG branch in SalesStateHandlerBase
- [ ] Unit tests: threshold filter, category fallback, fallback reply branches
- [ ] Integration test: 4 discovery transcript cases
- [ ] Manual smoke test against running app via Messenger sandbox page
- [ ] Update docs (codebase-summary, system-architecture, project-changelog)
- [ ] Code review via `code-reviewer` agent
- [ ] Merge to master with flag OFF, enable in staging, monitor 48h, ramp prod

## Success criteria
- ✅ Transcript `khách: shop có sản phẩm nào về da không` produces category menu, not SKU request.
- ✅ All existing 916 tests still pass.
- ✅ 4 new discovery transcript tests pass.
- ✅ Feature flag toggles cleanly (verified by integration test with flag off matching old behavior).
- ✅ No latency regression (p95 < +10ms).

## Risk assessment

| Risk | Mitigation |
|---|---|
| Threshold too strict → legitimate matches dropped | Default `MinSimilarityScore=0.50` is permissive; Phase 03 tunes via dataset. |
| Category menu becomes noisy reply | Cap at 5 categories; use emoji bullets; reuse existing copy tone. |
| Feature flag drift between envs | Default OFF in `appsettings.json`, document in `docs/deployment-guide.md`. |
| Customer asks for non-skincare category (e.g., "có serum không cho tóc") | Phase 01 only handles top-level categories; Phase 02 coarse router handles negation/scope. |

## Security considerations
- Tenant isolation: threshold filter runs **after** Pinecone namespace filter — no cross-tenant leak.
- No new PII captured; logs include only counts (dropped result count), not customer text.

## Next steps
- Pass Phase 01 review → start Phase 02 (two-stage classifier).
- Collect 1 week of `CategoryBrowseFallback` invocation logs to feed Phase 03 dataset.
