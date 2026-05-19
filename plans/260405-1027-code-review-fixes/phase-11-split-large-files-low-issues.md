---
phase: 11
title: "M2: Split Large Files + LOW Priority Issues"
priority: P3 (Low/Medium)
status: pending
depends_on: 07
---

## Overview
Split remaining large files (excluding Phase 07 targets) and fix LOW priority issues.

## Files to Modify
- `src/MessengerWebhook/Services/GeminiService.cs` (508 lines)
- `src/MessengerWebhook/Data/MessengerBotDbContext.cs` (459 lines)
- `src/MessengerWebhook/Services/Admin/AdminAuthService.cs` (383 lines)
- `src/MessengerWebhook/Services/PineconeVectorService.cs` (303 lines)
- `src/MessengerWebhook/WebhookProcessor.cs` (289 lines)
- `src/MessengerWebhook/Services/LiveCommentAutomationService.cs` (214 lines)
- `src/MessengerWebhook/Services/MessengerService.cs` (225 lines)
- Test files for L2/L3 warnings

## Implementation Steps

### File Splitting (pick top 3-4 offenders only, YAGNI)

1. **Split GeminiService.cs (508 lines)**
   - `Services/AI/GeminiTextService.cs` - text completion and chat
   - `Services/AI/GeminiEmbeddingService.cs` - embedding generation
   - `Services/AI/GeminiService.cs` - reduce to orchestrator/facade (<150 lines)
   - Move shared configuration constants to `Services/AI/GeminiConstants.cs`

2. **Split MessengerBotDbContext.cs (459 lines)**
   - `Data/Configurations/SessionConfiguration.cs` - extract model builder config
   - `Data/Configurations/ProductConfiguration.cs`
   - `Data/Configurations/OrderConfiguration.cs`
   - Keep DbContext class itself clean (<150 lines)

3. **Split LiveCommentAutomationService.cs (214 lines)**
   - `Services/LiveCommentAutomationService.cs` - keep core logic
   - `Services/LiveCommentResponseBuilder.cs` - move response generation

4. **Split MessengerService.cs (225 lines)**
   - `Services/MessengerClient.cs` - HTTP client layer (URL building, auth headers)
   - `Services/MessengerService.cs` - business logic and message formatting
   - Keep MessengerService <150 lines

### LOW Priority Issues

5. **L2: Fix CS8602 null reference warnings in tests**
   - Enable nullable reference types in test projects
   - Fix null pattern assertions with proper `!` assertions or `Assert.NotNull`

6. **L3: Fix xUnit1012 in WebhookEventDeserializationTests.cs**
   - Replace `null` pattern with proper null assertion

7. **L4: Extract Vietnamese strings** (defer - low ROI for single-language app)
   - Skip for now, document as known issue in TODO comment

8. **L5: DbContextModelSnapshot 1,571 lines**
   - Generated file, no action needed
   - Consider squash migrations in future maintenance sprint

## Success Criteria
- All 6 split files under 200 lines
- No build warnings for null reference in tests
- Test suite passes with no CS8602 or xUnit1012 warnings
- No behavior changes

## Risk Assessment
- **Likelihood:** Medium - many files to touch
- **Impact:** Low if done carefully - splitting is purely structural
- **Mitigation:** Test after each file split. Do Git history review to ensure no logic changes.

## Rollback
Revert commits one by one. Each split is structurally isolated.
