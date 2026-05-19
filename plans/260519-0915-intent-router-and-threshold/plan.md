# Plan: Intent Router + Adaptive Threshold for Sales Bot

**Created:** 2026-05-19
**Owner:** phus
**Status:** Draft → Approval pending
**Branch (target):** `feat/intent-router-threshold` (off master)

## Problem
Conversation transcript 2026-05-19 09:07:

```
khách: shop có sản phẩm nào về da không
bot:   Dạ hiện em chưa tìm thấy dữ liệu sản phẩm phù hợp...
       Chị cho em tên hoặc mã sản phẩm cụ thể...
```

Shop is **skincare** — "da" is the **core category**. Bot fails by asking customer for SKU code on a category-discovery query.

## Root cause (file:line evidence)
1. `Services/AI/GeminiService.cs:307-462` — intent classifier has 5 buckets focused on **purchase intent** (Browsing/Consulting/ReadyToBuy/Confirming/Questioning). **No `category_discovery` bucket** → "da" routes into RAG.
2. `Services/VectorSearch/PineconeVectorService.cs:209` — Pinecone score is **captured but never filtered**. Low-score garbage results passed downstream.
3. `Services/ProductGrounding/ProductGroundingService.cs:22` — fallback `FallbackReply` asks for SKU code; no category-listing fallback for short browsing queries.
4. `appsettings.json:143-148` — RAG section has only `TopK:5`, **no similarity threshold config**.

## Solution — 3 phases

| Phase | Goal | Risk | Duration |
|---|---|---|---|
| **[Phase 01](phase-01-quick-fix-threshold-and-fallback.md)** | Quick fix: similarity threshold + category-listing fallback + skip-RAG for short browsing queries. Feature-flag gated. | Low | 1-2 days |
| **[Phase 02](phase-02-two-stage-intent-router.md)** | Two-stage intent classifier (Coarse: `category_discovery / product_lookup / policy / order / small_talk / greeting`; Fine: keep existing 5 buckets). | Medium | 3-5 days |
| **[Phase 03](phase-03-adaptive-threshold-and-query-expansion.md)** | Adaptive threshold (per query length / intent) + LLM query expansion. Tune via dataset extracted from prod conversation history. | Medium | 2-3 days |

## Dependencies
- Phase 01 → standalone (feature flag)
- Phase 02 → depends on Phase 01 (uses threshold + fallback infra)
- Phase 03 → depends on Phase 02 (uses coarse intent) AND dataset script (see [`scripts/extract-messenger-queries.sql`](scripts/extract-messenger-queries.sql))

## Success metrics
- Bot answers "shop có sản phẩm nào về da không" with **category list**, not SKU request — 100% of new test transcripts.
- Empty/low-score RAG result rate < 5% on test dataset (currently unknown — baseline measured in Phase 03).
- No regression on existing 916/916 unit + integration tests.
- Live transcript test (`plans/260506-1411-live-ai-rag-transcript-test/`) extended with discovery scenarios — all pass.

## Rollout
- **Phase 01:** feature flag `RAG:Threshold:Enabled` (default `false` in prod, `true` in staging) → ramp to 100% after 48h clean.
- **Phase 02 & 03:** same gating pattern via `Intent:CoarseRouter:Enabled` and `RAG:AdaptiveThreshold:Enabled`.

## Resolved decisions (2026-05-19)
- ✅ Phase 02: **piggyback** coarse + fine in single Gemini call (~200-400ms saved per request).
- ✅ Phase 03: **per-intent metric** — Precision-first (≥0.85) for product_lookup, F1 for category_discovery. Empty-rate ceiling 30%.

## Open questions
- Dataset PII: confirm `plans/260519-0915-intent-router-and-threshold/data/` added to `.gitignore` before extraction run?
