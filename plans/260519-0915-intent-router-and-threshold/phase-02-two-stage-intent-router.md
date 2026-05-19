# Phase 02 — Two-stage intent classifier (Coarse Router)

**Priority:** P1
**Status:** Not started (blocked by Phase 01)
**Feature flag:** `Intent:CoarseRouter:Enabled` (default `false`)
**Estimated:** 3-5 days

## Context links
- Plan overview: [plan.md](plan.md)
- Prior intent work: `plans/260331-1955-ai-intent-detection-system/`, `plans/260503-1147-ai-intent-detection/`
- Existing classifier: `Services/AI/GeminiService.cs:307-462` (DetectIntentAsync — fine bucket)
- Existing keyword detector: `Services/Sales/Intent/CommerceMsgIntentDetector.cs:30-140`

## Overview
Industry best practice (Rasa, Dialogflow, Shopify Sidekick, Klarna): split intent classification into **two tiers**:

- **Coarse** (routing decision): `greeting | category_discovery | product_lookup | policy | order_action | small_talk | unknown`
- **Fine** (purchase intent — existing): `Browsing | Consulting | ReadyToBuy | Confirming | Questioning`

Coarse runs **first**. Only `product_lookup` and `category_discovery` enter RAG. Other intents route to dedicated handlers, **avoiding wasted vector search cost**.

## Key insights
- Existing `CommerceMsgIntentDetector` already does keyword-first then merges AI signal — same pattern reused for Coarse.
- Gemini FlashLite call latency ≈ 200-400ms — we can either **piggyback** coarse onto same call as fine (1 call returns both) or split. Decision: **piggyback** (cheaper, same latency).
- Many "policy" queries (e.g., "ship bao nhiêu tiền", "đổi trả thế nào") currently leak into RAG and return irrelevant products — Coarse router fixes this.

## Requirements

### Functional
- F1: `ICoarseIntentClassifier` returns one of 7 coarse buckets + confidence ∈ [0,1].
- F2: Gemini prompt extended to return BOTH coarse AND fine in single JSON response.
- F3: Keyword pre-filter classifies obvious cases (greeting words, "shop bán gì", "có sản phẩm nào về X", "ship", "đổi trả") without LLM call.
- F4: Routing table: each coarse → specific handler; falls back to existing fine-intent pipeline if `unknown`.
- F5: When flag OFF → existing fine-only behaviour preserved bit-for-bit.

### Non-functional
- Coarse call adds 0ms latency (piggyback on existing Gemini call).
- Keyword fast-path covers ≥40% of messages (measured on dataset from Phase 03 script).
- 95%+ accuracy on coarse classification (manually annotated 50-sample test set).

## Architecture

```
Webhook → SalesHandler
            │
            ├─ KeywordCoarseClassifier (regex/dictionary, instant)
            │     │
            │     ├─ HighConf (≥0.9) ─→ route
            │     └─ LowConf ───────────┐
            │                           ▼
            ├─ GeminiCombinedClassifier (1 call returns {coarse, fine})
            │     │
            │     └─ MergeKeyword + AI ─→ final coarse intent
            │
            ▼
        Coarse intent router:
        ├─ greeting          → GreetingHandler (existing)
        ├─ category_discovery → CategoryBrowseFallback (from Phase 01)
        ├─ product_lookup    → RAG → ProductGrounding (existing)
        ├─ policy            → PolicyGuard (existing — Services/Policy/)
        ├─ order_action      → OrderHandler (existing)
        ├─ small_talk        → SmallTalkHandler (existing)
        └─ unknown           → Fine classifier fallback (existing path)
```

## Related code files

### Modify
- `src/MessengerWebhook/Services/AI/GeminiService.cs` (line 307-462): extend `DetectIntentAsync` JSON schema to include `coarse_intent` field; update prompt to instruct dual-output.
- `src/MessengerWebhook/Services/Sales/Intent/CommerceMsgIntentDetector.cs` (line 30-140): rename or extend to also emit coarse classification.
- `src/MessengerWebhook/Services/Sales/SalesStateHandlerBase.cs` (line 306+): replace single-intent branch with coarse router switch.
- `src/MessengerWebhook/appsettings.json`: add `Intent:CoarseRouter` section.

### Create
- `src/MessengerWebhook/Services/Sales/Intent/CoarseIntent.cs` (enum).
- `src/MessengerWebhook/Services/Sales/Intent/ICoarseIntentClassifier.cs`.
- `src/MessengerWebhook/Services/Sales/Intent/KeywordCoarseClassifier.cs` (fast-path dictionary; ≤200 LOC).
- `src/MessengerWebhook/Services/Sales/Intent/HybridCoarseIntentClassifier.cs` (merges keyword + AI; ≤150 LOC).
- `src/MessengerWebhook/Services/Sales/Intent/CoarseIntentRouter.cs` (maps coarse → handler; ≤150 LOC).
- Test files mirroring each.

### Delete
- None (keep existing fine classifier as fallback path).

## Implementation steps

1. **Define `CoarseIntent` enum** + interface.
   → verify: build clean.

2. **Build `KeywordCoarseClassifier`** with dictionaries:
   - greeting: ["xin chào", "chào", "hi", "hello", "alo"]
   - category_discovery: ["có sản phẩm nào", "shop bán gì", "có gì cho", "tư vấn cho", "muốn xem"] AND token count ≤6
   - policy: ["ship", "phí ship", "đổi trả", "bảo hành", "thanh toán", "hoàn tiền"]
   - order_action: ["chốt đơn", "đặt hàng", "mua", "lấy size", "đơn của em"]
   - small_talk: ["cảm ơn", "thanks", "ok ạ", "vâng", "dạ"]
   → verify: 30+ unit test cases, each keyword → expected bucket.

3. **Extend Gemini prompt** for combined output:
   ```json
   {
     "coarse_intent": "category_discovery|product_lookup|...|unknown",
     "coarse_confidence": 0.0-1.0,
     "fine_intent": "Browsing|Consulting|...",
     "fine_confidence": 0.0-1.0
   }
   ```
   → verify: existing fine-intent tests still pass; new coarse field parsed.

4. **Implement `HybridCoarseIntentClassifier`**:
   - If keyword conf ≥ 0.9 → return keyword result, skip AI.
   - Else → call combined Gemini, merge: `final_conf = max(keyword_conf, ai_conf)`, prefer AI when disagreement and ai_conf ≥ 0.7.
   → verify: hybrid unit tests cover (keyword-only, AI-only, agreement, disagreement).

5. **Build `CoarseIntentRouter`** with switch:
   - Inject all 6 handlers (greeting, category-browse, product-lookup, policy, order, small-talk).
   - Route by `CoarseIntent` enum.
   - Fallback to existing fine-intent pipeline on `Unknown`.
   → verify: routing unit tests.

6. **Wire router** into `SalesStateHandlerBase`:
   - Behind `Intent:CoarseRouter:Enabled` flag.
   - If flag OFF → existing path.
   - If flag ON → router decides.
   → verify: existing tests pass with flag OFF; new integration tests pass with flag ON.

7. **Add regression transcripts**:
   - "shop bán gì ạ" → category_discovery
   - "ship bao nhiêu" → policy
   - "chốt đơn cho em" → order_action
   - "cảm ơn shop" → small_talk
   → verify: 4+ new transcript tests pass.

8. **Cost/latency measurement**: log coarse path timings, measure on staging for 24h.
   → verify: p95 latency report unchanged or improved.

9. **Docs sync**: update `docs/system-architecture.md` with router diagram.
   → verify: `docs-manager` review.

## Todo
- [ ] Define CoarseIntent enum + interface
- [ ] KeywordCoarseClassifier + 30+ unit tests
- [ ] Extend Gemini prompt for dual output, update JSON parser
- [ ] HybridCoarseIntentClassifier + unit tests
- [ ] CoarseIntentRouter + routing unit tests
- [ ] Wire feature-flagged branch in SalesStateHandlerBase
- [ ] DI registration in Program.cs / DI extension class
- [ ] 4+ transcript regression tests for non-discovery intents
- [ ] Latency benchmark on staging
- [ ] Docs sync via docs-manager
- [ ] code-reviewer pass
- [ ] Enable flag on staging, monitor 72h, ramp prod

## Success criteria
- ✅ Discovery query "shop bán gì" + "có sản phẩm nào về X" route to CategoryBrowseFallback, NOT RAG.
- ✅ Policy queries ("ship", "đổi trả") route to PolicyGuard, NOT RAG.
- ✅ Order queries route to OrderHandler.
- ✅ Coarse classifier accuracy ≥ 95% on manually annotated 50-sample test set.
- ✅ Gemini token cost per request unchanged (piggybacked, same call).
- ✅ All existing tests still pass.

## Risk assessment

| Risk | Mitigation |
|---|---|
| Gemini returns malformed combined JSON | Existing fenced-JSON regression tests (`plans/260419-2250-gemini-fenced-json-regression-tests/`) already harden parser; extend with coarse field. |
| Coarse misroutes ambiguous queries | Confidence threshold + fallback to fine-intent pipeline on `Unknown`. |
| Keyword dictionary maintenance burden | Use config file (JSON), not hardcoded — easy to tune without redeploy. |
| Behavioural drift when flag flipped | Pin a golden-transcript test suite that runs against BOTH flag states. |

## Security considerations
- No new PII handling; message text already flows through existing PII redactor.
- Coarse classification logs use redacted text + bucket name only.

## Next steps
- Once Phase 02 stable on prod → Phase 03 builds adaptive threshold on top of `coarse_intent` (different thresholds per intent class).
