---
phase: 01
title: "C1: Restore Startup Config Validation"
priority: P1 (Critical)
status: pending
depends_on: none
---

## Overview
Re-enable commented-out config validation at startup to ensure fail-fast behavior when required keys are missing.

## Files to Modify
- `src/MessengerWebhook/Program.cs` (lines 397-402)
- `src/MessengerWebhook/Options/FacebookOptions.cs` (add `IValidateOptions<T>`)
- `src/MessengerWebhook/Options/WebhookOptions.cs` (add `IValidateOptions<T>`)

## Implementation Steps

1. **Create `IValidateOptions` validators**
   - File: `src/MessengerWebhook/Options/ValidateFacebookOptions.cs` (new)
   - Implement `IValidateOptions<FacebookOptions>`:
     - Validate `AppSecret` is non-whitespace
     - Validate `PageAccessToken` OR log warning if DB override exists
     - Return `ValidateOptionsResult.Fail(...)` with clear messages
   - File: `src/MessengerWebhook/Options/ValidateWebhookOptions.cs` (new)
     - Validate `VerifyToken` is non-whitespace

2. **Register validators in `Program.cs`**
   - Replace commented-out `if` checks with `.AddOptions<FacebookOptions>().ValidateEagerly()`
   - `.AddOptions<WebhookOptions>().ValidateEagerly()`
   - Remove the commented-out manual checks

3. **Handle development mode**
   - In `app.Environment.IsDevelopment()`, use `.ValidateOnStart()` with graceful warnings instead of hard failures
   - In production, use `.ValidateEagerly()` which throws

## Success Criteria
- App fails immediately on startup without required config
- Development mode shows warnings but continues
- No commented-out validation code remains
- `dotnet build` succeeds

## Risk Assessment
- **Likelihood:** Low - purely additive change
- **Impact:** Medium if dev environment lacks env vars
- **Mitigation:** Graceful dev-mode warnings, update `.env.example` with all required keys

## Rollback
Revert the single commit. Re-comment the validation lines temporarily.
