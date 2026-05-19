# Phase 03 — Adaptive threshold + LLM query expansion

**Priority:** P2
**Status:** Not started (blocked by Phase 01 + 02)
**Feature flag:** `RAG:AdaptiveThreshold:Enabled`, `RAG:QueryExpansion:Enabled`
**Estimated:** 2-3 days (+ tuning time)

## Context links
- Plan overview: [plan.md](plan.md)
- Phase 01 (threshold infra): [phase-01-quick-fix-threshold-and-fallback.md](phase-01-quick-fix-threshold-and-fallback.md)
- Phase 02 (coarse intent): [phase-02-two-stage-intent-router.md](phase-02-two-stage-intent-router.md)
- Dataset extraction script: [scripts/extract-messenger-queries.sql](scripts/extract-messenger-queries.sql)
- Related research: `plans/reports/researcher-260401-1311-vietnamese-embedding-benchmark.md`

## Overview
Replace fixed `MinSimilarityScore=0.50` (from Phase 01) with **adaptive threshold**:
- Threshold varies by `(query_length_bucket, coarse_intent)`.
- For very short queries (1-2 tokens) → trigger **LLM query expansion** before vector search.
- Tune thresholds via grid sweep on dataset extracted from prod conversation history.

## Key insights
- Vietnamese embeddings (Gemini `text-embedding-004` or Vertex) underperform on single-token queries — confirmed by `researcher-260401-1311-vietnamese-embedding-benchmark.md`.
- Query expansion (LLM rewrites "da" → "chăm sóc da, kem dưỡng da, serum da, ...") improves recall ~30% on similar benchmarks (Algolia neural search blog 2024, Cohere paper).
- BM25 keyword search (already in `HybridSearchService`) partly compensates — but only for exact term match; "da" matches almost nothing.
- Cohere rerank (Phase 09 prior work) helps ordering but doesn't help when initial retrieval misses → expansion solves the retrieval gap.

## Requirements

### Functional
- F1: Threshold lookup table loaded from `appsettings.json` keyed by `(queryLengthBucket, coarseIntent)`.
- F2: Buckets: `Short(1-2 tokens) | Medium(3-5) | Long(6+)`.
- F3: `IQueryExpander` rewrites short queries via Gemini Flash (cheap model) into 3-5 alternative phrasings; results merged before embedding.
- F4: Each variant embedded once (parallel), max-pooled scores per product, then filtered by adaptive threshold.
- F5: When `RAG:QueryExpansion:Enabled=false` → standard single-query path.
- F6: When `RAG:AdaptiveThreshold:Enabled=false` → fall back to Phase 01 fixed threshold.

### Non-functional
- Query expansion adds ≤300ms p95 (parallel Gemini Flash call, cached on identical query for 10min via existing Redis).
- F1 score ≥ 0.75 on tuning dataset; precision ≥ 0.80 (prefer precision over recall — wrong product worse than no product).
- Expansion call cost < $0.001 per message (Gemini Flash 2.5 pricing at Jan 2026).

## Architecture

```
Coarse intent = product_lookup OR category_discovery
                       │
                       ▼
        ┌──────────────────────────────┐
        │ Query length bucket?         │
        ├─ Short(1-2) ─→ QueryExpander → [original, variant1..N]
        ├─ Medium(3-5) ─→ [original]
        └─ Long(6+)   ─→ [original]
                       │
                       ▼
              Embed each (parallel)
                       │
                       ▼
        Vector search per variant → max-pool scores
                       │
                       ▼
        Adaptive threshold lookup:
        threshold = config[(bucket, coarseIntent)]
                       │
                       ▼
        Filter scores < threshold → RRF fuse with keyword → rerank
```

## Threshold config (initial — Phase 03 step 6 tunes these)

```jsonc
"RAG": {
  "AdaptiveThreshold": {
    "Enabled": false,
    "Buckets": {
      "Short_ProductLookup":  0.65,
      "Short_CategoryDiscovery": 0.55,
      "Medium_ProductLookup": 0.70,
      "Medium_CategoryDiscovery": 0.60,
      "Long_ProductLookup":   0.75,
      "Long_CategoryDiscovery": 0.65
    },
    "DefaultForUnknownBucket": 0.50
  },
  "QueryExpansion": {
    "Enabled": false,
    "VariantCount": 4,
    "Model": "gemini-2.5-flash",
    "CacheTtlMinutes": 10
  }
}
```

## Related code files

### Modify
- `src/MessengerWebhook/Services/VectorSearch/HybridSearchService.cs`: accept multi-variant queries, max-pool scores.
- `src/MessengerWebhook/Services/VectorSearch/PineconeVectorService.cs`: extend filter from Phase 01 to take dynamic threshold per call.
- `src/MessengerWebhook/Services/Configuration/RagOptions.cs`: nested `AdaptiveThreshold`, `QueryExpansion` options classes.

### Create
- `src/MessengerWebhook/Services/RAG/QueryExpansion/IQueryExpander.cs`.
- `src/MessengerWebhook/Services/RAG/QueryExpansion/GeminiQueryExpander.cs` (≤180 LOC).
- `src/MessengerWebhook/Services/RAG/QueryExpansion/QueryExpansionPrompt.cs` (template).
- `src/MessengerWebhook/Services/RAG/Threshold/AdaptiveThresholdResolver.cs` (≤100 LOC).
- `src/MessengerWebhook/Services/RAG/Threshold/QueryLengthBucket.cs` (enum).
- Tests for each.
- **Tuning script:** `plans/260519-0915-intent-router-and-threshold/scripts/threshold-sweep.py` (off-repo execution; reads dataset CSV, sweeps thresholds, outputs CSV report).

### Delete
- None.

## Implementation steps

1. **Extract dataset** from prod DB:
   - Run `scripts/extract-messenger-queries.sql` against tenant `mui-xu` (or all tenants) → outputs CSV.
   - Annotate manually (~1-2 hours for 50 samples): each row labeled with `expected_coarse_intent`, `expected_product_ids` (if applicable).
   - Store annotated CSV at `plans/260519-0915-intent-router-and-threshold/data/annotated-queries.csv` (gitignore the raw extract for PII).
   → verify: ≥50 annotated rows covering all 7 coarse intents.

2. **Implement `AdaptiveThresholdResolver`**:
   - `ResolveThreshold(int tokenCount, CoarseIntent intent) → float`.
   - Reads from `RagOptions.AdaptiveThreshold.Buckets`.
   - Falls back to `DefaultForUnknownBucket` on miss.
   → verify: unit tests for each bucket combo.

3. **Implement `GeminiQueryExpander`**:
   - Prompt: "Cho query '{query}' về sản phẩm skincare, viết 4 phrasings thay thế (mỗi dòng 1 phrasing, không đánh số, không giải thích)."
   - Parses lines, dedupes, returns ≤4 variants.
   - Caches result in Redis 10min using normalized query as key.
   → verify: integration test against real Gemini (env-gated, follows pattern from `plans/260506-1411-live-ai-rag-transcript-test/`).

4. **Integrate into HybridSearchService**:
   - Accept `IEnumerable<string> queryVariants`.
   - Embed each in parallel.
   - For each variant → Pinecone search → score per product.
   - Max-pool: `final_score[product] = max(scores across variants)`.
   - Apply adaptive threshold using `(longest_variant_tokens, coarseIntent)`.
   → verify: HybridSearchService tests extended with multi-variant cases.

5. **Wire feature flags**:
   - In RAG entry point, if `QueryExpansion:Enabled` AND token count ≤2 → call expander → pass variants.
   - Else → pass single original query (wrapped in list).
   - If `AdaptiveThreshold:Enabled` → use resolver; else use Phase 01 fixed threshold.
   → verify: A/B integration tests cover all 4 flag combos.

6. **Tune thresholds** via grid sweep:
   - Run `scripts/threshold-sweep.py` (uses `.claude/skills/.venv/bin/python3`).
   - Sweep range: 0.40 → 0.80 step 0.05 per bucket.
   - Metric: precision@5, recall@5, F1.
   - Output: CSV with best threshold per bucket.
   - Apply winning values to `appsettings.json`.
   → verify: F1 ≥ 0.75 on validation set (20% holdout from annotated dataset).

7. **Live transcript regression**:
   - Extend `plans/260506-1411-live-ai-rag-transcript-test/` with adaptive-threshold cases.
   - Compare flag-on vs flag-off transcripts side-by-side.
   → verify: flag-on transcripts equal or better on manual review.

8. **Cost monitoring**:
   - Add metric `RagQueryExpansionCallsPerHour` to existing observability (`docs/sla-runbook.md`).
   - Alert if call rate exceeds expected ceiling (suggests cache miss issue).
   → verify: dashboard shows green for 24h.

9. **Docs**:
   - `docs/system-architecture.md`: add adaptive threshold + expansion sections.
   - `docs/project-changelog.md`: log Phase 03.
   → verify: docs-manager review.

## Todo
- [ ] Run dataset extraction SQL → CSV → manual annotation
- [ ] AdaptiveThresholdResolver + unit tests
- [ ] QueryLengthBucket enum + bucketing helper
- [ ] GeminiQueryExpander + prompt template + Redis cache integration
- [ ] HybridSearchService multi-variant support + max-pool merge
- [ ] Feature flag wiring in RAG entry point
- [ ] DI registration
- [ ] Tuning script (Python) + sweep run + apply winning thresholds
- [ ] Live transcript regression tests
- [ ] Cost + latency observability metrics + dashboard panels
- [ ] Docs sync
- [ ] code-reviewer pass
- [ ] Staged rollout: 10% → 50% → 100% (24h between steps)

## Success criteria
- ✅ Annotated tuning dataset: ≥50 rows, ≥7 intent classes covered.
- ✅ F1 ≥ 0.75, precision ≥ 0.80 on validation holdout.
- ✅ Query "da" returns relevant skincare products (after expansion → "chăm sóc da, kem dưỡng da, serum, ...").
- ✅ p95 latency ≤ +300ms when expansion enabled (parallel + cached).
- ✅ Cost increase < $5/month at current message volume.
- ✅ All previous-phase tests still pass.

## Risk assessment

| Risk | Mitigation |
|---|---|
| Query expansion hallucinates irrelevant variants | Prompt explicitly constrained to skincare domain; ground-truth tests verify variant quality. |
| Adaptive threshold over-fitted to annotation bias | 80/20 train/val split; cross-validate; sanity-check on live transcripts before ramp. |
| Cache poisoning if normalized key collides | Normalize via lowercase + trim only (no aggressive transforms); test edge cases. |
| Cost spike if Redis cache fails | Circuit breaker: if expansion fails → fall back to original query, log warning. |
| Bucket boundaries (1-2 vs 3-5) too rigid | Make bucket boundaries config-driven; Phase 03+ could move to continuous adaptive function. |

## Security considerations
- Query expansion sends customer message text to Gemini → already in scope of existing data-processing agreement.
- Annotated dataset stored OUTSIDE git (PII): keep in `plans/.../data/` and add to `.gitignore` even though `plans/**/*` is now tracked.
- No new PII storage; tunings persisted only as scalar thresholds in `appsettings.json`.

## Open questions
- Should threshold tuning happen monthly (drift) or one-time? Defer to Phase 03+ once we have baseline data.
- Should we expand long queries too (paraphrase for better recall)? Current plan: NO — long queries already have enough signal; cost not justified. Revisit after Phase 03 metrics.

## Next steps
- Phase 03 done → measure 30 days, decide if continuous-adaptive (regression-based threshold) worth it as Phase 04.
- Consider sharing tuning dataset with Cohere rerank (Phase 09) for joint optimization.
