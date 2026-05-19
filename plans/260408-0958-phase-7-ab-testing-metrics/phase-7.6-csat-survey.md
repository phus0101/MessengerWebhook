---
phase: 7.6
title: "CSAT Survey"
effort: 3h
status: completed
dependencies: [7.2]
completion_date: 2026-04-09
---

# Phase 7.6: CSAT Survey

## Context

Add post-conversation CSAT (Customer Satisfaction) survey to measure user satisfaction and correlate with A/B test variants. Survey triggers 5 minutes after conversation completion.

**Related Files**:
- `src/MessengerWebhook/StateMachine/Handlers/CompleteStateHandler.cs` - Conversation completion
- `src/MessengerWebhook/Services/Metrics/ConversationMetricsService.cs` - Metrics collection (Phase 7.2)
- `src/MessengerWebhook/Services/Messenger/MessengerService.cs` - Send messages

## Overview

**Priority**: P1 (user decision: add CSAT survey)  
**Status**: Completed ✅  
**Effort**: 3 hours  
**Dependencies**: Phase 7.2 (Metrics Collection)  
**Completion Date**: 2026-04-09

## Key Insights

- 5-minute delay prevents survey from interrupting active conversations
- 5-star rating (1-5) is simple and universally understood
- Optional text feedback for low ratings (≤3) captures improvement opportunities
- Facebook Messenger quick reply buttons provide native UX
- CSAT score by variant enables A/B test comparison (control vs treatment)

## Requirements

### Functional

**Survey Trigger**:
- Trigger: Conversation state changes to `Complete`
- Delay: 5 minutes after completion (prevents interruption)
- Delivery: Facebook Messenger quick reply buttons
- One-time: Survey sent once per conversation session

**Survey Flow**:
```
Conversation Complete → Wait 5 minutes → Send Survey
    ↓
"Bạn đánh giá trải nghiệm tư vấn như thế nào?"
[⭐] [⭐⭐] [⭐⭐⭐] [⭐⭐⭐⭐] [⭐⭐⭐⭐⭐]
    ↓
User selects rating → Store in conversation_surveys
    ↓
If rating ≤ 3: "Bạn có thể chia sẻ thêm để chúng em cải thiện không?"
    ↓
User sends text feedback (optional) → Store feedback_text
    ↓
"Cảm ơn bạn đã đánh giá! Ý kiến của bạn giúp chúng em cải thiện dịch vụ."
```

**Survey Storage**:
- Store in `conversation_surveys` table
- Link to `conversation_sessions` via `session_id`
- Track A/B test variant for comparison
- Store rating (1-5) and optional feedback text

**Survey Metrics**:
- CSAT score = (4-5 star ratings / total ratings) × 100
- Average rating by variant (control vs treatment)
- Response rate (surveys sent vs surveys completed)
- Feedback themes (manual analysis of text feedback)

### Non-Functional

- Survey delivery latency: <500ms (Messenger API call)
- Background job for 5-minute delay (no blocking)
- Idempotent: Survey sent once per session (check `survey_sent` flag)
- Tenant isolation: Survey data filtered by `tenant_id`
- No impact on conversation flow (async background job)

## Architecture

### Data Flow

```
Conversation Complete → CompleteStateHandler
    ↓
Set session.State = Complete
    ↓
Schedule background job (5min delay)
    ↓
BackgroundJob: CSATSurveyService.SendSurveyAsync(sessionId)
    ↓
Check if survey already sent (session.survey_sent flag)
    ↓
If not sent: Send Messenger quick reply → Set survey_sent = true
    ↓
User clicks rating → Webhook receives postback
    ↓
SurveyStateHandler.HandleRatingAsync(psid, rating)
    ↓
Store in conversation_surveys table
    ↓
If rating ≤ 3: Send follow-up message for feedback
    ↓
User sends text → SurveyStateHandler.HandleFeedbackAsync(psid, text)
    ↓
Update conversation_surveys.feedback_text
    ↓
Send thank you message
```

### Database Schema

**New Table: conversation_surveys**

```sql
CREATE TABLE conversation_surveys (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES conversation_sessions(id) ON DELETE CASCADE,
    facebook_psid VARCHAR(255) NOT NULL,
    tenant_id UUID NOT NULL,
    
    -- A/B Test Context
    ab_test_variant VARCHAR(20), -- 'control' or 'treatment'
    
    -- Survey Data
    rating INT NOT NULL CHECK (rating BETWEEN 1 AND 5),
    feedback_text TEXT,
    
    -- Metadata
    created_at TIMESTAMP DEFAULT NOW(),
    
    -- Indexes
    CONSTRAINT fk_session FOREIGN KEY (session_id) REFERENCES conversation_sessions(id),
    CONSTRAINT fk_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id)
);

CREATE INDEX idx_conversation_surveys_session ON conversation_surveys(session_id);
CREATE INDEX idx_conversation_surveys_tenant ON conversation_surveys(tenant_id);
CREATE INDEX idx_conversation_surveys_variant ON conversation_surveys(ab_test_variant);
CREATE INDEX idx_conversation_surveys_rating ON conversation_surveys(rating);
CREATE INDEX idx_conversation_surveys_created ON conversation_surveys(created_at);
```

**Update: conversation_sessions table**

```sql
ALTER TABLE conversation_sessions 
ADD COLUMN survey_sent BOOLEAN DEFAULT FALSE;
```

### Messenger Quick Reply Format

```json
{
  "recipient": { "id": "{{PSID}}" },
  "message": {
    "text": "Bạn đánh giá trải nghiệm tư vấn như thế nào?",
    "quick_replies": [
      {
        "content_type": "text",
        "title": "⭐",
        "payload": "CSAT_RATING_1"
      },
      {
        "content_type": "text",
        "title": "⭐⭐",
        "payload": "CSAT_RATING_2"
      },
      {
        "content_type": "text",
        "title": "⭐⭐⭐",
        "payload": "CSAT_RATING_3"
      },
      {
        "content_type": "text",
        "title": "⭐⭐⭐⭐",
        "payload": "CSAT_RATING_4"
      },
      {
        "content_type": "text",
        "title": "⭐⭐⭐⭐⭐",
        "payload": "CSAT_RATING_5"
      }
    ]
  }
}
```

## Related Code Files

### Files to Create

**1. Services (3 files)**
- `src/MessengerWebhook/Services/Survey/ICSATSurveyService.cs` - Interface
- `src/MessengerWebhook/Services/Survey/CSATSurveyService.cs` - Implementation
- `src/MessengerWebhook/Services/Survey/Models/SurveyResponse.cs` - DTO

**2. State Handler (1 file)**
- `src/MessengerWebhook/StateMachine/Handlers/SurveyStateHandler.cs` - Handle survey responses

**3. Background Job (1 file)**
- `src/MessengerWebhook/BackgroundJobs/SendCSATSurveyJob.cs` - Delayed survey delivery

**4. Entity (1 file)**
- `src/MessengerWebhook/Data/Entities/ConversationSurvey.cs` - Survey entity

**5. Migration (1 file)**
- `src/MessengerWebhook/Data/Migrations/AddConversationSurveys.cs` - Database migration

### Files to Modify

**1. `src/MessengerWebhook/StateMachine/Handlers/CompleteStateHandler.cs`**
- Schedule survey background job after 5 minutes

**2. `src/MessengerWebhook/Data/Entities/ConversationSession.cs`**
- Add `SurveySent` property

**3. `src/MessengerWebhook/Data/MessengerBotDbContext.cs`**
- Add `ConversationSurveys` DbSet
- Configure entity relationships

**4. `src/MessengerWebhook/Program.cs`**
- Register `ICSATSurveyService`
- Register background job scheduler

**5. `src/MessengerWebhook/appsettings.json`**
- Add CSAT survey configuration

## Implementation Steps

### Step 1: Database Migration (30min)

**Create Migration**:
```bash
dotnet ef migrations add AddConversationSurveys --project src/MessengerWebhook
```

**Migration Content**:
- Create `conversation_surveys` table
- Add `survey_sent` column to `conversation_sessions`
- Create indexes

### Step 2: Create Survey Entity (15min)

**File**: `Data/Entities/ConversationSurvey.cs`

```csharp
public class ConversationSurvey
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string FacebookPsid { get; set; }
    public Guid TenantId { get; set; }
    public string? ABTestVariant { get; set; }
    public int Rating { get; set; } // 1-5
    public string? FeedbackText { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public ConversationSession Session { get; set; }
}
```

### Step 3: Create Survey Service (45min)

**Interface**: `Services/Survey/ICSATSurveyService.cs`

```csharp
public interface ICSATSurveyService
{
    Task SendSurveyAsync(Guid sessionId);
    Task HandleRatingAsync(string psid, int rating);
    Task HandleFeedbackAsync(string psid, string feedbackText);
}
```

**Implementation**: `Services/Survey/CSATSurveyService.cs`

Key methods:
- `SendSurveyAsync()` - Send quick reply with 5 star options
- `HandleRatingAsync()` - Store rating, send follow-up if ≤3
- `HandleFeedbackAsync()` - Store feedback text, send thank you

### Step 4: Create Survey State Handler (30min)

**File**: `StateMachine/Handlers/SurveyStateHandler.cs`

Handle webhook postbacks:
- `CSAT_RATING_1` through `CSAT_RATING_5` payloads
- Text messages when awaiting feedback

### Step 5: Create Background Job (30min)

**File**: `BackgroundJobs/SendCSATSurveyJob.cs`

```csharp
public class SendCSATSurveyJob : IHostedService
{
    public async Task ExecuteAsync(Guid sessionId, TimeSpan delay)
    {
        await Task.Delay(delay); // 5 minutes
        await _surveyService.SendSurveyAsync(sessionId);
    }
}
```

### Step 6: Integrate with CompleteStateHandler (20min)

**File**: `StateMachine/Handlers/CompleteStateHandler.cs`

```csharp
public override async Task<StateTransitionResult> HandleAsync(...)
{
    // ... existing completion logic
    
    // Schedule CSAT survey (5min delay)
    _backgroundJobClient.Schedule<SendCSATSurveyJob>(
        job => job.ExecuteAsync(session.Id, TimeSpan.FromMinutes(5)),
        TimeSpan.FromMinutes(5)
    );
    
    return StateTransitionResult.Success();
}
```

### Step 7: Update Metrics Dashboard (10min)

**File**: `AdminApp/src/pages/Metrics/ABTestSummary.tsx` (Phase 7.5)

Add CSAT score card:
- Average CSAT score by variant
- Response rate
- Distribution chart (1-5 stars)

### Step 8: Testing (30min)

- Unit test: `CSATSurveyServiceTests.cs`
- Integration test: End-to-end survey flow
- Manual test: Complete conversation → Wait 5min → Verify survey sent

## Todo List

- [ ] Create `AddConversationSurveys` migration
- [ ] Run migration: `dotnet ef database update`
- [ ] Create `ConversationSurvey` entity
- [ ] Update `ConversationSession` entity (add `SurveySent`)
- [ ] Update `MessengerBotDbContext` (add DbSet)
- [ ] Create `ICSATSurveyService` interface
- [ ] Implement `CSATSurveyService`
- [ ] Create `SurveyResponse` DTO
- [ ] Create `SurveyStateHandler`
- [ ] Create `SendCSATSurveyJob` background job
- [ ] Update `CompleteStateHandler` (schedule survey)
- [ ] Register services in `Program.cs`
- [ ] Add CSAT config to `appsettings.json`
- [ ] Write unit tests for `CSATSurveyService`
- [ ] Write integration test for survey flow
- [ ] Update metrics dashboard (Phase 7.5) to show CSAT
- [ ] Manual test: Complete conversation and verify survey
- [ ] Verify 5-minute delay works
- [ ] Verify follow-up for low ratings (≤3)
- [ ] Verify tenant isolation

## Success Criteria

- [ ] Survey sent 5 minutes after conversation completion
- [ ] Quick reply buttons render correctly in Messenger
- [ ] Rating (1-5) stored in `conversation_surveys` table
- [ ] Follow-up message sent for ratings ≤3
- [ ] Feedback text stored correctly
- [ ] Thank you message sent after feedback
- [ ] Survey sent only once per session (`survey_sent` flag)
- [ ] CSAT score visible in dashboard (Phase 7.5)
- [ ] A/B test variant tracked for each survey
- [ ] Tenant isolation enforced
- [ ] No impact on conversation flow (async background job)
- [ ] All tests passing

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Survey interrupts active conversations | Low | High | 5-minute delay, check session state before sending |
| Low response rate (<20%) | Medium | Medium | Keep survey simple (5 stars), send at optimal time |
| Background job fails silently | Low | Medium | Add logging, retry logic, monitor job queue |
| User sends text instead of clicking button | Medium | Low | Handle text input gracefully, prompt to use buttons |
| Survey sent multiple times | Low | High | Idempotency check (`survey_sent` flag) |

## Security Considerations

- **Tenant Isolation**: Survey data filtered by `tenant_id`
- **Input Validation**: Rating must be 1-5, feedback text max 500 chars
- **Rate Limiting**: One survey per session (idempotency)
- **PII Protection**: No sensitive user data in survey (only PSID)
- **SQL Injection**: Use parameterized queries (EF Core handles this)

## Backwards Compatibility

- New table `conversation_surveys` (no impact on existing tables)
- New column `survey_sent` in `conversation_sessions` (default FALSE)
- Existing sessions without `survey_sent` → Treated as FALSE
- Survey feature optional (can be disabled via config)
- No breaking changes to existing APIs

## Configuration

**appsettings.json**:

```json
{
  "CSATSurvey": {
    "Enabled": true,
    "DelayMinutes": 5,
    "SendFollowUpForLowRatings": true,
    "LowRatingThreshold": 3,
    "Messages": {
      "SurveyQuestion": "Bạn đánh giá trải nghiệm tư vấn như thế nào?",
      "FollowUpQuestion": "Bạn có thể chia sẻ thêm để chúng em cải thiện không?",
      "ThankYou": "Cảm ơn bạn đã đánh giá! Ý kiến của bạn giúp chúng em cải thiện dịch vụ."
    }
  }
}
```

## Rollback Plan

**Rollback Steps**:
1. Set `CSATSurvey.Enabled = false` in config
2. Stop background job scheduler
3. (Optional) Drop `conversation_surveys` table
4. (Optional) Remove `survey_sent` column from `conversation_sessions`

**Data Impact**: Survey data retained (read-only), no new surveys sent

**Database Rollback**:
```sql
DROP TABLE conversation_surveys;
ALTER TABLE conversation_sessions DROP COLUMN survey_sent;
```

## Metrics to Track

**Survey Metrics**:
- Response rate: (surveys completed / surveys sent) × 100
- CSAT score: (4-5 star ratings / total ratings) × 100
- Average rating: Mean of all ratings (1-5)
- Distribution: Count of each rating (1, 2, 3, 4, 5)

**A/B Test Comparison**:
- CSAT score by variant (control vs treatment)
- Response rate by variant
- Average rating by variant
- Statistical significance (t-test, p-value)

**Dashboard Integration** (Phase 7.5):
- CSAT card in A/B Test Summary view
- Trend chart in Conversation Outcomes view
- Export to CSV for external analysis

## Next Steps

1. Complete Phase 7.2 (Metrics Collection) first
2. Implement survey service and state handler
3. Test survey flow end-to-end
4. Integrate CSAT metrics into dashboard (Phase 7.5)
5. Run 2-week A/B test with CSAT tracking
6. Analyze CSAT correlation with naturalness pipeline

## Unresolved Questions

1. **Survey timing**: 5 minutes optimal or adjust based on data? (Recommendation: Start with 5min, adjust if response rate <20%)
2. **Follow-up for high ratings**: Ask for testimonial if rating = 5? (Recommendation: Defer to Phase 8, keep simple for now)
3. **Multi-language support**: Vietnamese only or add English? (Recommendation: Vietnamese only, add i18n later)
4. **Survey frequency**: One per session or one per user per month? (Recommendation: One per session, prevents survey fatigue)
5. **Feedback analysis**: Manual review or sentiment analysis? (Recommendation: Manual review first, automate if volume >100/week)
