---
status: pending
priority: high
created: 2026-04-06
timeline: 1-2 weeks
scope: full-implementation
blockedBy: []
blocks: []
---

# Plan: Bot Naturalness Improvements

## Executive Summary

Cải thiện tính tự nhiên của chatbot để giao tiếp giống nhân viên bán hàng thật - vui vẻ, thân thiện nhưng chuyên nghiệp. Giải quyết vấn đề khách quen nhắn casual ("hi sốp") nhưng bot trả lời formal với giới thiệu catalog đầy đủ.

**Timeline:** 1-2 tuần (thorough implementation với A/B testing)  
**Scope:** Full implementation (Priority 0 + 1 + 2)

## Problem Statement

### Current Issues

1. **Greeting không tự nhiên**: Khách quen gửi "hi sốp" → Bot trả lời formal "Dạ em chào chị khách quen của Múi Xù ạ. Chị đang quan tâm sản phẩm nào ạ?"
2. **Không mirror tone**: Bot không bắt chước tone của khách (casual vs formal)
3. **Thiếu personality**: System prompt không có personality traits (Big Five OCEAN)
4. **Không có emotion detection**: Không nhận biết cảm xúc khách hàng
5. **Response quá dài**: Thiếu validation về độ dài câu trả lời

### Root Causes

- `BuildVipInstruction` (SalesStateHandlerBase.cs:581-629) chỉ xử lý VIP, bỏ qua khách `Returning`
- `GreetingStyle` (CustomerIntelligenceService.cs:99-103) là hardcoded string thay vì instruction
- Không có tone analysis trước khi generate response
- Thiếu emotion detection service

## Research Findings

Dựa trên 2 research reports:
- **Emotion Detection**: Hybrid approach (rule-based + ML) đạt 85%+ accuracy
- **Vietnamese Greeting Patterns**: Pronoun selection critical, tone transition framework
- **Best Practices**: Lazada 80%+ automation, Rockship 35% revenue boost với natural chatbot

## Architecture Overview

```
User Input → EmotionDetectionService → ConversationContextAnalyzer 
→ ToneMatchingService → SalesStateHandlerBase (Enhanced) 
→ ResponseValidator → Output
```

## Implementation Phases

### Phase 0: Foundation & Personality (Days 1-2) - PRIORITY 0
**Goal:** Fix immediate greeting issue và add personality traits

**Files:**
- `CustomerIntelligenceService.cs` (lines 99-103)
- `SalesStateHandlerBase.cs` (lines 581-629)
- `Prompts/personality-traits.txt` (NEW)

**Success Criteria:**
- ✅ Khách quen không còn nhận catalog intro
- ✅ Personality traits visible trong responses
- ✅ Tone matching instruction trong prompt

### Phase 1: Emotion Detection Service (Days 3-4) - PRIORITY 1
**Goal:** Implement hybrid emotion detection (rule-based + ML ready)

**New Services:**
- `Services/Emotion/EmotionDetectionService.cs`
- `Services/Emotion/Models/EmotionScore.cs`
- `Services/Emotion/IEmotionDetectionService.cs`

**Success Criteria:**
- ✅ Emotion detection accuracy: 85%+ (rule-based baseline)
- ✅ Response time: < 100ms
- ✅ Integration point ready cho ML model

### Phase 2: Tone Matching Service (Days 4-5) - PRIORITY 1
**Goal:** Dynamic tone adaptation based on emotion + customer context

**New Services:**
- `Services/Tone/ToneMatchingService.cs`
- `Services/Tone/Models/ToneProfile.cs`
- `Services/Tone/IToneMatchingService.cs`

**Success Criteria:**
- ✅ Tone matching appropriateness: 90%+
- ✅ Pronoun selection correct: 95%+
- ✅ Emotion-based escalation working

### Phase 3: Conversation Context Analyzer (Days 5-6) - PRIORITY 1
**Goal:** Analyze conversation history for better context awareness

**New Services:**
- `Services/Conversation/ConversationContextAnalyzer.cs`
- `Services/Conversation/Models/ConversationContext.cs`

**Success Criteria:**
- ✅ Context analysis accuracy: 85%+
- ✅ Pattern detection working
- ✅ Performance: < 50ms for 10-turn history

### Phase 4: Small Talk & Natural Flow (Day 6) - PRIORITY 2
**Goal:** Add small talk capability và improve conversation flow

**New Services:**
- `Services/SmallTalk/SmallTalkService.cs`

**Success Criteria:**
- ✅ Small talk handled naturally: 80%+
- ✅ Smooth transition to business conversation

### Phase 5: Response Validation (Day 7) - PRIORITY 2
**Goal:** Validate response quality before sending

**New Services:**
- `Services/Validation/ResponseValidator.cs`
- `Services/Validation/Models/ValidationResult.cs`

**Success Criteria:**
- ✅ Response length appropriate: 90%+
- ✅ Tone consistency validated
- ✅ Pronoun usage correct

### Phase 6: Integration & Testing (Days 7-8) - PRIORITY 2
**Goal:** Wire everything together và comprehensive testing

**Tasks:**
- Integrate all services vào `SalesStateHandlerBase`
- Update DI registration trong `Program.cs`
- Write unit tests cho tất cả services
- Write integration tests cho key flows

**Success Criteria:**
- ✅ All unit tests passing (85%+ coverage)
- ✅ Integration tests passing
- ✅ No performance regression (< 2s response time)

### Phase 7: A/B Testing & Metrics (Days 8+) - PRIORITY 2
**Goal:** A/B testing infrastructure và monitoring

**New Services:**
- `Services/Metrics/ConversationMetricsService.cs`
- Database migration: `ConversationMetric` table

**Success Criteria:**
- ✅ A/B testing framework operational
- ✅ Metrics dashboard showing emotion accuracy, tone matching rate
- ✅ CSAT tracking enabled

## Success Metrics

**Immediate (Phase 0):**
- Khách quen không còn nhận catalog introduction
- Personality traits visible trong responses
- Customer satisfaction feedback positive

**Week 1 (Phases 1-3):**
- Emotion detection accuracy: 85%+
- Tone matching appropriateness: 90%+
- Pronoun selection correct: 95%+

**Week 2 (Phases 4-7):**
- Conversation completion rate: 70%+
- Small talk handled naturally: 80%+
- Response length appropriate: 90%+
- All tests passing (85%+ coverage)

## Risk Assessment

### High Risk
1. **ML Model Integration Complexity** - Mitigation: Start với rule-based, ML là optional
2. **Performance Impact** - Mitigation: Async processing, caching, monitoring

### Medium Risk
3. **Pronoun Selection Accuracy** - Mitigation: Default to neutral "bạn" when uncertain
4. **Tone Drift in Long Conversations** - Mitigation: History limit, validation

### Low Risk
5. **Response Validation Overhead** - Mitigation: Lightweight rules (< 10ms)

## Dependencies

### External
- Hugging Face Transformers (Phase 1b, optional)
- Redis (existing)
- PostgreSQL (existing)

### Internal
- CustomerIntelligenceService (existing)
- GeminiService (existing)
- ConversationSession (existing)
- SalesBotOptions (existing)

## Related Plans

- `plans/improve-returning-customer-naturalness/` - Superseded by Phase 0 của plan này

## Next Steps

1. Review plan với team
2. Approve Phase 0 implementation
3. Start với `CustomerIntelligenceService.cs` và `SalesStateHandlerBase.cs` fixes
4. Continue với Phases 1-7 theo timeline

## References

- Research Report: `plans/reports/researcher-emotion-detection-260406-2036.md`
- Research Report: `plans/reports/researcher-greeting-patterns-260406-2036.md`
