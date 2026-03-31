# Hybrid AI + Rule-Based Confirmation Detection

**Date**: 2026-03-31 19:26
**Severity**: Medium
**Component**: Sales State Machine / Message Parser
**Status**: Resolved
**Commit**: 8e1ad26

## What Happened

Pure keyword matching for order confirmations was generating false positives. Messages like "ship bao lau?" (how long to ship?) triggered confirmation detection because they contained shipping-related keywords, causing premature state transitions and broken conversation flows.

## The Brutal Truth

We shipped a sales bot that couldn't distinguish between a customer asking a question and confirming an order. The keyword list approach was fundamentally flawed—it treated language as a bag of words instead of understanding intent. Every false positive meant a lost sale opportunity and frustrated customer.

## Technical Details

**Before**: `SalesMessageParser.ParseMessage()` used synchronous keyword matching:
- Checked for "xác nhận", "đồng ý", "ok", "ship", etc.
- No context awareness
- No confidence scoring
- False positive rate: ~15-20% on ambiguous messages

**After**: Hybrid system with three-tier detection:

1. **Fast Path (80% cases, 0ms latency)**:
   - Explicit phone regex: `\b0\d{9}\b`
   - Address extraction: "địa chỉ: ...", "gửi đến ...", "ship về ..."
   - Rejects questions: "?", "bao lâu", "khi nào"
   - Returns `IsConfirmation=true` immediately

2. **Ambiguity Detection**:
   - Triggers AI when: keywords present + question markers OR no explicit data
   - Example: "ship bao lau?" → AI call
   - Example: "ok ship về 123 Nguyen Trai" → fast path

3. **AI Reasoning (Gemini FlashLite)**:
   - Prompt: "Is this Vietnamese message confirming an order?"
   - Response: JSON with `isConfirmation` (bool) + `confidence` (0-1) + `reasoning` (string)
   - Timeout: 500ms
   - Confidence threshold: 0.7
   - Fallback: Conservative (no confirmation) on error

**Code changes**:
- `GeminiService.DetectConfirmationAsync()`: New method with structured output
- `SalesMessageParser.ParseMessageAsync()`: Refactored to async, hybrid logic
- `SalesStateHandlerBase.HandleMessageAsync()`: Updated to call async parser
- `ConfirmationDetectionResult`: New model with confidence + reasoning
- Feature flag: `EnableAiConfirmationDetection` (default: true)

**Test coverage**: 25/25 unit tests passing
- `ConsultingStateHandlerTests`: Mocked GeminiService
- `SalesMessageParserTests`: Fast path + AI path scenarios
- Edge cases: Timeouts, low confidence, ambiguous messages

## What We Tried

1. **Expanded keyword list**: Made false positives worse
2. **Negative keywords**: Brittle, couldn't cover all question patterns
3. **Regex-only extraction**: Missed implicit confirmations like "ok đồng ý"
4. **Pure AI**: Too slow (200-300ms per message), cost concerns

## Root Cause Analysis

Treated natural language understanding as a pattern matching problem. Vietnamese is context-heavy—"ship" can mean "to ship" (verb) or "shipping" (noun in question). Keywords alone can't capture intent without semantic understanding.

## Lessons Learned

- **Hybrid > Pure**: Combine fast heuristics with AI for best latency/accuracy tradeoff
- **Confidence scoring matters**: Binary yes/no isn't enough—need probability for edge cases
- **Feature flags are essential**: Rollback capability for AI-dependent features
- **Cost modeling upfront**: $0.36/month is negligible, but we should've calculated this before implementation
- **Async all the way**: Mixing sync/async parsers caused refactoring pain—should've been async from day one

## Impact

**Positive**:
- False positive rate: ~15% → <2% (estimated, needs production validation)
- Latency: 80% fast path (0ms), 20% AI path (200-300ms avg)
- Cost: $0.36/month with 60% cache hit rate
- Rollback safety: Feature flag + conservative fallback

**Risks**:
- AI timeout/error handling untested in production load
- Confidence threshold (0.7) is arbitrary—may need tuning
- Cache hit rate assumption (60%) unvalidated

## Next Steps

1. **Production monitoring** (Week 1):
   - Track false positive/negative rates via admin dashboard
   - Monitor AI call latency (p50, p95, p99)
   - Validate cache hit rate assumption

2. **Threshold tuning** (Week 2):
   - A/B test confidence thresholds: 0.6, 0.7, 0.8
   - Adjust based on precision/recall metrics

3. **Fallback testing** (Week 1):
   - Simulate Gemini API outages
   - Verify conservative fallback behavior

4. **Cost validation** (Month 1):
   - Compare actual costs vs. $0.36/month estimate
   - Optimize prompt if costs exceed budget

**Owner**: Backend team
**Timeline**: Production deploy + 2 weeks monitoring
