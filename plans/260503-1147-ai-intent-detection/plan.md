---
title: "AI-Based Intent Detection System"
description: "Hybrid keyword-first + AI fallback for sub-intent classification"
status: completed
priority: P1
effort: 16h
branch: master
tags: [ai, intent-detection, gemini, conversation]
created: 2026-05-03
---

# AI-Based Intent Detection Implementation Plan

## Overview

Replace keyword-only `TopicAnalyzer` with hybrid keyword-first + AI fallback architecture for sub-intent classification in conversational chatbot.

**Architecture:** Keyword matching (fast path <10ms) → AI classifier (fallback ~510ms)

**Performance Target:** <500ms for 70% of queries, 3-5s for ambiguous 30%

**Cost Target:** ~$0.075/month (30% AI usage, 70% keyword-only)

## Sub-Intent Categories

- `product_question` - công dụng, thành phần, cách dùng
- `price_question` - giá, giảm giá
- `shipping_question` - ship, vận chuyển
- `policy_question` - quà tặng, khuyến mãi, đổi trả
- `availability_question` - còn hàng, tồn kho
- `comparison_question` - so sánh sản phẩm
- `none` - no specific sub-intent

## Implementation Phases

| Phase | Description | Effort | Status |
|-------|-------------|--------|--------|
| [Phase 1](phase-01-core-interfaces-and-models.md) | Core interfaces & models | 2h | completed |
| [Phase 2](phase-02-keyword-detector.md) | Keyword detector (fast path) | 2h | completed |
| [Phase 3](phase-03-gemini-classifier.md) | Gemini AI classifier (fallback) | 3h | completed |
| [Phase 4](phase-04-hybrid-orchestrator.md) | Hybrid orchestrator | 2h | completed |
| [Phase 5](phase-05-configuration-and-di.md) | Configuration & DI setup | 2h | completed |
| [Phase 6](phase-06-integration.md) | Integration with state handlers | 2h | completed |
| [Phase 7](phase-07-testing.md) | Unit & integration tests | 3h | completed |

## Key Design Decisions

**Pattern Reference:** `GeminiPolicyIntentClassifier` (proven in production)

**Confidence Thresholds:**
- Keyword high confidence: ≥0.9 (accept immediately)
- AI acceptance threshold: ≥0.7 (use AI result)
- AI low confidence: <0.5 (fallback to keyword)

**Timeout Strategy:**
- AI classifier: 500ms hard limit
- Fallback: keyword result or default intent

**Model Selection:**
- Model: `FlashLiteModel` (gemini-2.5-flash-lite)
- Temperature: 0.1 (deterministic)
- Max tokens: 150 (JSON response)

## Success Criteria

- [x] p95 latency <1s (vs 3-5s pure AI)
- [x] Accuracy ≥85% (vs 70% keyword-only)
- [x] Cost <$1/month (vs $3/month pure AI)
- [x] Error rate <5% (timeout + API errors)
- [x] All tests pass (unit + integration) - **20/20 unit tests + 3 integration tests passing**
- [x] No breaking changes to existing handlers

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Latency regression | Hybrid approach, 500ms timeout, keyword fallback |
| Cost overrun | 30% AI usage cap, circuit breaker pattern |
| Gemini API outage | Fallback to keywords, retry logic |
| Accuracy degradation | Parallel deployment, manual review, A/B test |

## Dependencies

- Existing: `GeminiService`, `GeminiOptions`, `SalesBotOptions`
- New: `SubIntentOptions` configuration section
- Pattern: `GeminiPolicyIntentClassifier` (reference implementation)

## Migration Strategy

1. **Week 1-2:** Parallel deployment (log both results, no behavior change)
2. **Week 3-4:** Shadow mode (use AI when confidence ≥0.8, log disagreements)
3. **Week 5:** A/B test (50% keyword vs 50% hybrid)
4. **Week 6:** Full rollout if metrics pass

## Files to Create

```
src/MessengerWebhook/Services/SubIntent/
├── ISubIntentClassifier.cs
├── SubIntentResult.cs
├── SubIntentCategory.cs
├── KeywordSubIntentDetector.cs
├── GeminiSubIntentClassifier.cs
└── HybridSubIntentClassifier.cs

src/MessengerWebhook/Configuration/
└── SubIntentOptions.cs

tests/MessengerWebhook.UnitTests/Services/SubIntent/
├── KeywordSubIntentDetectorTests.cs
├── GeminiSubIntentClassifierTests.cs
└── HybridSubIntentClassifierTests.cs
```

## Files to Modify

- `src/MessengerWebhook/Program.cs` - register services
- `src/MessengerWebhook/appsettings.json` - add SubIntent config
- `src/MessengerWebhook/StateMachine/Handlers/ConsultingStateHandler.cs` - use classifier
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` - inject classifier

## Next Steps

1. Review research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md`
2. Start with Phase 1: Core interfaces & models
3. Follow phases sequentially (each phase blocks next)
4. Run tests after each phase
5. Update docs after Phase 7

## References

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md`
- Pattern reference: `src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs`
- Existing keyword system: `src/MessengerWebhook/Services/Conversation/TopicAnalyzer.cs`
