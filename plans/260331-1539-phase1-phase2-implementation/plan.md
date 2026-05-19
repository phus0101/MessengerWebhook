---
title: "Phase 1 & 2: Email Notifications, Risk Message Fix, System Prompt Enhancement, Livestream Automation, Bot Lock Unlock"
description: "Implement SMTP email notifications, sanitize risk messages, enhance system prompt, add livestream automation, and complete bot lock unlock endpoint"
status: pending
priority: P1
effort: 5d
branch: master
tags: [email, security, ai-prompt, livestream, bot-lock]
created: 2026-03-31
---

# Implementation Plan: Phase 1 & 2

## Overview

**Phase 1 (1-2 days):**
1. Implement email notification with SMTP
2. Fix risk message to prevent internal assessment exposure
3. Enhance system prompt with stronger policy instructions

**Phase 2 (3-5 days):**
4. Implement livestream automation
5. Complete bot lock mechanism with unlock endpoint

## Context

- **Project:** Facebook Messenger Sales Bot (MessengerWebhook)
- **Tech Stack:** ASP.NET Core, Entity Framework Core, Gemini AI
- **Working Directory:** `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook`
- **Reports Path:** `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\plans\reports\`

## Phase 1: Email, Risk Message, System Prompt

### Task 1.1: Implement Email Notification with SMTP

**Status:** Ready to implement
**Effort:** 4h
**Priority:** P1

#### Current State Analysis

- `EmailNotificationService` already exists with full SMTP implementation
- Uses `System.Net.Mail.SmtpClient` with SSL support
- Configuration via `EmailOptions` (Host, Port, Username, Password, FromAddress, etc.)
- Sends HTML emails with support case details and completion links
- Token-based security via `ISupportCaseTokenService`

#### Implementation Steps

1. **Verify SMTP configuration in appsettings.json**
   - Ensure `Email` section has all required fields
   - Add environment-specific overrides in appsettings.Development.json

2. **Test email sending functionality**
   - Create integration test for `EmailNotificationService.SendSupportCaseAssignedAsync`
   - Mock SMTP server or use test email service (e.g., Ethereal)
   - Verify HTML template rendering
   - Verify token generation and validation

3. **Add error handling and logging**
   - Wrap SMTP calls in try-catch with detailed logging
   - Handle common SMTP errors (authentication, connection timeout, invalid recipient)
   - Add retry logic for transient failures

4. **Update documentation**
   - Document SMTP configuration requirements
   - Add troubleshooting guide for common email issues

#### Files to Modify

- `src/MessengerWebhook/appsettings.json` - Add Email configuration section
- `src/MessengerWebhook/Configuration/EmailOptions.cs` - Already complete
- `src/MessengerWebhook/Services/Support/EmailNotificationService.cs` - Add error handling
- `tests/MessengerWebhook.IntegrationTests/EmailNotificationTests.cs` - Create new test file

#### Success Criteria

- [ ] SMTP configuration properly set in appsettings.json
- [ ] Email sends successfully to assigned manager
- [ ] HTML template renders correctly with all dynamic fields
- [ ] Token validation works for completion links
- [ ] Integration tests pass with 100% coverage
- [ ] Error scenarios handled gracefully with logging

#### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| SMTP credentials exposed in config | Medium | High | Use environment variables, add to .gitignore |
| Email delivery failures | Medium | Medium | Add retry logic, log failures, fallback notification |
| HTML rendering issues | Low | Low | Test across email clients, use simple HTML |

---

### Task 1.2: Fix Risk Message to Prevent Internal Assessment Exposure

**Status:** Ready to implement
**Effort:** 2h
**Priority:** P1

#### Current State Analysis

- `CustomerIntelligenceService.BuildRiskSignalAsync` creates `RiskSignal` with internal reason:
  - High risk: "Customer has elevated delivery risk and should be manually reviewed"
  - Low risk: "No critical risk detected"
- This reason is stored in database and potentially exposed to customers
- Risk assessment includes `Score`, `Level`, `Source`, `RequiresManualReview`

#### Problem

The current implementation stores technical risk assessment details that should never be shown to customers. If these messages leak into customer-facing responses, it exposes:
- Internal risk scoring logic
- Business rules about delivery failures
- Manual review triggers

#### Implementation Steps

1. **Separate internal and customer-facing messages**
   - Keep internal `Reason` field for staff/admin use
   - Add `CustomerMessage` field for safe customer-facing text
   - Update `RiskSignal` entity with new field

2. **Sanitize risk messages in customer responses**
   - Create `RiskMessageSanitizer` service
   - Map internal risk levels to generic customer messages
   - Ensure no risk assessment details leak into bot responses

3. **Update database schema**
   - Add migration for `CustomerMessage` column
   - Backfill existing records with safe default messages

4. **Update all risk message usage points**
   - Review all places where `RiskSignal.Reason` is used
   - Ensure customer-facing code uses `CustomerMessage`
   - Admin/internal code can continue using `Reason`

#### Files to Modify

- `src/MessengerWebhook/Data/Entities/RiskSignal.cs` - Add `CustomerMessage` property
- `src/MessengerWebhook/Services/Customers/CustomerIntelligenceService.cs` - Update risk signal creation
- `src/MessengerWebhook/Services/Support/RiskMessageSanitizer.cs` - Create new service
- `src/MessengerWebhook/Data/Migrations/YYYYMMDDHHMMSS_AddCustomerMessageToRiskSignal.cs` - New migration
- `src/MessengerWebhook/StateMachine/Handlers/*StateHandler.cs` - Review all handlers for risk message usage

#### Success Criteria

- [ ] `RiskSignal` entity has separate `Reason` (internal) and `CustomerMessage` (safe) fields
- [ ] Migration applied successfully
- [ ] All customer-facing code uses sanitized messages
- [ ] Admin dashboard shows internal reason
- [ ] No risk assessment details exposed in bot responses
- [ ] Unit tests verify message sanitization

#### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Existing messages already exposed | High | Medium | Audit logs, notify affected customers if needed |
| Migration breaks existing queries | Low | High | Test migration thoroughly, add rollback script |
| Sanitized messages too generic | Medium | Low | Balance safety with helpfulness, iterate based on feedback |

---

### Task 1.3: Enhance System Prompt with Stronger Policy Instructions

**Status:** Ready to implement
**Effort:** 3h
**Priority:** P1

#### Current State Analysis

Current system prompt (`sales-closer-system-prompt.txt`) has:
- Basic policy rules (no free shipping, no extra discounts, no refunds/cancellations)
- VIP detection only changes tone, not pricing
- Generic escalation for policy violations

#### Problems

- Rules are stated but not strongly enforced
- No explicit consequences for policy violations
- Lacks examples of what NOT to do
- Missing edge case handling instructions

#### Implementation Steps

1. **Strengthen policy enforcement language**
   - Change from "Không tự ý hứa" to "NGHIÊM CẤM tự ý hứa"
   - Add explicit consequences: "Vi phạm = chuyển nhân viên ngay lập tức"
   - Use imperative, non-negotiable language

2. **Add negative examples (anti-patterns)**
   - Show examples of BAD responses that violate policy
   - Explain why each example is wrong
   - Contrast with correct responses

3. **Add edge case handling rules**
   - Customer asks for discount: "Em xin lỗi chị, giá này là giá tốt nhất rồi ạ. Chị gửi em SĐT và địa chỉ để em lên đơn nha."
   - Customer asks for free shipping: "Dạ phí ship theo chính sách của shop ạ. Em lên đơn ngay cho chị, chị gửi em SĐT và địa chỉ nha."
   - Customer asks to cancel/refund: "Dạ em xin phép chuyển nhân viên hỗ trợ chị về vấn đề này ạ."

4. **Add policy violation detection instructions**
   - Instruct AI to self-check before responding
   - If response contains policy violation keywords, rewrite or escalate
   - Add internal validation checklist

5. **Test with adversarial prompts**
   - Create test cases with tricky customer requests
   - Verify AI doesn't violate policy under pressure
   - Iterate prompt based on failures

#### Files to Modify

- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt` - Enhance with stronger rules
- `tests/MessengerWebhook.IntegrationTests/SystemPromptPolicyTests.cs` - Create new test file
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs` - Add post-response validation

#### Enhanced Prompt Structure

```
QUY TẮC BẮT BUỘC (NGHIÊM CẤM VI PHẠM):

1. NGHIÊM CẤM tự ý hứa:
   - Miễn phí ship
   - Thêm quà tặng ngoài chính sách
   - Giảm giá thêm
   - Hoàn tiền
   - Hủy đơn

   VI PHẠM = CHUYỂN NHÂN VIÊN NGAY LẬP TỨC

2. Nếu khách yêu cầu bất kỳ điều trên:
   - KHÔNG giải thích dài dòng
   - KHÔNG từ chối trực tiếp
   - NÓI: "Dạ em xin phép chuyển nhân viên hỗ trợ chị về vấn đề này ạ."
   - GỌI: EscalateToCaseManagement()

3. VIP chỉ đổi giọng điệu, KHÔNG đổi chính sách giá

ANTI-PATTERNS (TUYỆT ĐỐI TRÁNH):

❌ XẤU: "Dạ để em xin phép sếp giảm thêm cho chị nha."
✅ TỐT: "Dạ giá này là giá tốt nhất rồi ạ. Chị gửi em SĐT và địa chỉ nha."

❌ XẤU: "Chị mua 2 em miễn phí ship cho chị luôn nha."
✅ TỐT: "Dạ phí ship theo chính sách shop ạ. Em lên đơn cho chị, chị gửi SĐT và địa chỉ nha."

❌ XẤU: "Dạ chị yên tâm, nếu không vừa ý em hoàn tiền cho chị."
✅ TỐT: "Dạ sản phẩm có chính sách đổi trả theo quy định ạ. Chị gửi em SĐT và địa chỉ nha."
```

#### Success Criteria

- [ ] System prompt has explicit policy enforcement language
- [ ] Anti-patterns section added with 5+ examples
- [ ] Edge case handling rules documented
- [ ] Self-validation checklist included
- [ ] Integration tests pass with adversarial prompts
- [ ] No policy violations in 100 test conversations

#### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Prompt too restrictive, AI refuses valid requests | Medium | Medium | Balance strictness with flexibility, iterate based on false positives |
| AI finds loopholes in new rules | Low | High | Continuous monitoring, add rules as loopholes discovered |
| Prompt length exceeds token limit | Low | Low | Keep concise, remove redundancy |

---

## Phase 2: Livestream Automation & Bot Lock Unlock

### Task 2.1: Implement Livestream Automation

**Status:** Ready to implement
**Effort:** 2d
**Priority:** P2

#### Current State Analysis

- `LiveCommentAutomationService` already exists with core functionality:
  - Keyword-based comment filtering
  - Rate limiting (50 comments/minute)
  - Idempotency check
  - Bot lock check
  - Welcome message with quick reply buttons
  - Auto-hide comments
  - Conversation session creation

#### Missing Components

1. **Facebook Live webhook integration**
   - Need to subscribe to `live_comments` webhook event
   - Parse comment payload (comment_id, commenter_psid, text, video_id)
   - Route to `LiveCommentAutomationService`

2. **Comment reply functionality**
   - Currently only sends private message
   - Need to add public comment reply option
   - Use Facebook Graph API `/comment_id/replies` endpoint

3. **Configuration and testing**
   - Add livestream-specific configuration
   - Test with Facebook Live test events
   - Monitor rate limits and error handling

#### Implementation Steps

1. **Add Facebook Live webhook subscription**
   - Update `WebhookProcessor` to handle `live_comments` event
   - Parse comment payload and extract required fields
   - Route to `LiveCommentAutomationService.ProcessCommentAsync`

2. **Implement comment reply functionality**
   - Add `IMessengerService.ReplyToCommentAsync(commentId, text)` method
   - Use Graph API `POST /{comment-id}/replies` with `message` parameter
   - Handle rate limits and errors

3. **Add configuration options**
   - `LiveCommentOptions.EnablePublicReply` - toggle public vs private reply
   - `LiveCommentOptions.PublicReplyTemplate` - template for public replies
   - `LiveCommentOptions.MaxRepliesPerVideo` - prevent spam

4. **Enhance rate limiting**
   - Add per-video rate limiting
   - Add per-user rate limiting
   - Add global rate limiting for all livestreams

5. **Add monitoring and logging**
   - Log all processed comments
   - Track success/failure rates
   - Alert on rate limit violations

6. **Test with Facebook Live**
   - Create test livestream
   - Post test comments with trigger keywords
   - Verify bot responds correctly
   - Test rate limiting and error scenarios

#### Files to Modify

- `src/MessengerWebhook/Services/WebhookProcessor.cs` - Add live_comments event handling
- `src/MessengerWebhook/Services/MessengerService.cs` - Add ReplyToCommentAsync method
- `src/MessengerWebhook/Services/LiveComments/LiveCommentAutomationService.cs` - Add public reply option
- `src/MessengerWebhook/Configuration/LiveCommentOptions.cs` - Add new config options
- `src/MessengerWebhook/appsettings.json` - Add LiveComment configuration
- `tests/MessengerWebhook.IntegrationTests/LiveCommentAutomationTests.cs` - Create new test file

#### Data Flow

```
Facebook Live Comment
  ↓
Webhook Event (live_comments)
  ↓
WebhookProcessor.ProcessAsync()
  ↓
LiveCommentAutomationService.ProcessCommentAsync()
  ↓
├─ Check trigger keywords → Skip if no match
├─ Check rate limits → Skip if exceeded
├─ Check idempotency → Skip if already processed
├─ Check bot lock → Skip if locked
├─ Send welcome message (private or public)
├─ Hide comment (optional)
└─ Create conversation session
```

#### Success Criteria

- [ ] Webhook receives and processes live_comments events
- [ ] Bot replies to comments with trigger keywords
- [ ] Rate limiting prevents spam
- [ ] Idempotency prevents duplicate processing
- [ ] Bot lock prevents replies to locked users
- [ ] Comments auto-hide when configured
- [ ] Conversation sessions created correctly
- [ ] Integration tests pass with mock Facebook API
- [ ] Manual testing on real livestream successful

#### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Facebook rate limits hit during viral livestream | High | High | Implement aggressive rate limiting, queue comments, prioritize VIP |
| Comment spam overwhelms system | Medium | High | Add per-user rate limits, blacklist abusive users |
| Webhook delivery delays | Medium | Medium | Add timeout handling, retry failed comments |
| Public replies expose bot behavior | Low | Medium | Make replies natural, avoid bot-like patterns |

---

### Task 2.2: Complete Bot Lock Mechanism with Unlock Endpoint

**Status:** Ready to implement
**Effort:** 1d
**Priority:** P2

#### Current State Analysis

- `BotLockService` already exists with full functionality:
  - `LockAsync` - locks bot for user
  - `ReleaseAsync` - unlocks bot for user
  - `ExtendLockAsync` - extends lock duration
  - `IsLockedAsync` - checks lock status
  - `GetActiveLocksAsync` - lists all active locks
  - `GetLockHistoryAsync` - shows lock history

- Unlock endpoint exists at `/internal/support-cases/{id}/complete`:
  - GET with token validation (email link)
  - POST with resolution notes (API call)
  - Both unlock bot after resolving support case

#### Missing Components

1. **Direct unlock endpoint without support case**
   - Current unlock requires support case ID
   - Need endpoint to unlock by PSID directly
   - Use case: Manual unlock by admin without case

2. **Admin dashboard integration**
   - Need UI to view active locks
   - Need UI to manually unlock users
   - Need UI to extend lock duration

3. **Automatic unlock on timeout**
   - Background service to auto-unlock expired locks
   - Configurable timeout (default 120 minutes)
   - Notification when auto-unlocked

#### Implementation Steps

1. **Add direct unlock endpoint**
   - `POST /internal/bot-locks/{psid}/unlock` - unlock by PSID
   - Require admin authentication
   - Add audit logging
   - Return lock history after unlock

2. **Add lock management endpoints**
   - `GET /internal/bot-locks` - list all active locks
   - `GET /internal/bot-locks/{psid}` - get lock details
   - `POST /internal/bot-locks/{psid}/extend` - extend lock duration
   - `DELETE /internal/bot-locks/{psid}` - force unlock

3. **Create background service for auto-unlock**
   - `BotLockCleanupService` - runs every 5 minutes
   - Query locks where `UnlockAt < DateTime.UtcNow`
   - Call `BotLockService.ReleaseAsync` for each
   - Log auto-unlock events

4. **Add admin dashboard UI**
   - Active locks table with PSID, reason, unlock time
   - Unlock button for each lock
   - Extend lock button with duration picker
   - Lock history view

5. **Add audit logging**
   - Log all lock/unlock operations
   - Include actor (system, admin, email link)
   - Include reason and timestamp
   - Store in `AdminAuditLog` table

6. **Test unlock scenarios**
   - Manual unlock via API
   - Auto-unlock on timeout
   - Unlock via email link
   - Unlock via support case completion
   - Verify bot resumes conversation after unlock

#### Files to Modify

- `src/MessengerWebhook/Endpoints/InternalOperationsEndpointExtensions.cs` - Add unlock endpoints
- `src/MessengerWebhook/BackgroundServices/BotLockCleanupService.cs` - Already exists, verify functionality
- `src/MessengerWebhook/Services/Support/BotLockService.cs` - Add audit logging
- `src/MessengerWebhook/AdminApp/src/pages/bot-locks-page.tsx` - Create new admin page
- `tests/MessengerWebhook.IntegrationTests/BotLockTests.cs` - Create new test file

#### API Endpoints

```
GET    /internal/bot-locks                    - List all active locks
GET    /internal/bot-locks/{psid}             - Get lock details
POST   /internal/bot-locks/{psid}/unlock      - Unlock by PSID
POST   /internal/bot-locks/{psid}/extend      - Extend lock duration
DELETE /internal/bot-locks/{psid}             - Force unlock (admin only)
```

#### Success Criteria

- [ ] Direct unlock endpoint works without support case
- [ ] Admin dashboard shows active locks
- [ ] Manual unlock via dashboard works
- [ ] Auto-unlock on timeout works
- [ ] All unlock operations logged in audit log
- [ ] Bot resumes conversation after unlock
- [ ] Integration tests cover all unlock scenarios
- [ ] Background service runs reliably

#### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Auto-unlock while human still helping | Medium | Medium | Add "extend lock" button in email, notify before auto-unlock |
| Unlock endpoint abused by attackers | Low | High | Require admin authentication, rate limit, audit all operations |
| Background service fails silently | Low | Medium | Add health checks, alert on failures, log all operations |

---

## Testing Strategy

### Unit Tests

- `EmailNotificationService` - SMTP sending, error handling
- `RiskMessageSanitizer` - Message sanitization logic
- `PolicyGuardService` - Policy violation detection
- `LiveCommentAutomationService` - Comment processing logic
- `BotLockService` - Lock/unlock operations

### Integration Tests

- Email sending end-to-end with test SMTP server
- Risk message sanitization in full conversation flow
- System prompt policy enforcement with adversarial prompts
- Livestream comment processing with mock Facebook API
- Bot lock/unlock with database persistence

### Manual Testing

- Send test email and verify HTML rendering
- Test bot responses with policy violation attempts
- Create test livestream and post comments
- Lock/unlock bot via admin dashboard
- Verify auto-unlock on timeout

---

## Rollback Plan

### Phase 1 Rollback

1. **Email notifications:** Disable via `EmailOptions.Host = ""` in config
2. **Risk message fix:** Revert migration, use old `Reason` field
3. **System prompt:** Revert to previous version in git

### Phase 2 Rollback

1. **Livestream automation:** Disable via `LiveCommentOptions.Enabled = false`
2. **Bot lock unlock:** Remove new endpoints, keep existing functionality

---

## Success Metrics

### Phase 1

- Email delivery rate > 95%
- Zero risk assessment leaks in customer messages
- Policy violation rate < 1% in production conversations

### Phase 2

- Livestream comment response rate > 90%
- Average unlock time < 5 minutes (manual) or 120 minutes (auto)
- Zero bot lock failures

---

## Unresolved Questions

1. **Email SMTP provider:** Which SMTP service to use? (Gmail, SendGrid, AWS SES, etc.)
2. **Risk message backfill:** Should we backfill existing `RiskSignal` records with sanitized messages?
3. **Livestream rate limits:** What are Facebook's actual rate limits for comment replies?
4. **Bot lock notifications:** Should we notify customers when bot is locked/unlocked?
5. **Admin authentication:** What authentication mechanism for unlock endpoints? (JWT, API key, etc.)

---

## Dependencies

- Phase 1.1 (Email) → No dependencies
- Phase 1.2 (Risk message) → No dependencies
- Phase 1.3 (System prompt) → No dependencies
- Phase 2.1 (Livestream) → Requires Facebook Live webhook subscription
- Phase 2.2 (Bot lock unlock) → Requires admin authentication system

---

## Timeline

| Phase | Task | Duration | Start | End |
|-------|------|----------|-------|-----|
| 1.1 | Email notifications | 4h | Day 1 AM | Day 1 PM |
| 1.2 | Risk message fix | 2h | Day 1 PM | Day 1 PM |
| 1.3 | System prompt | 3h | Day 2 AM | Day 2 PM |
| 2.1 | Livestream automation | 2d | Day 3 | Day 4 |
| 2.2 | Bot lock unlock | 1d | Day 5 | Day 5 |

**Total:** 5 days (1-2 days Phase 1, 3-5 days Phase 2)
