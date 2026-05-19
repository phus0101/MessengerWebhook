---
phase: 02
title: "C4: Move Access Token to Bearer Header"
priority: P1 (Critical)
status: pending
depends_on: none
---

## Overview
Move PageAccessToken from query string to Authorization Bearer header in all Facebook Graph API calls to prevent token leakage in proxy logs.

## Files to Modify
- `src/MessengerWebhook/Services/MessengerService.cs` (lines 44, 85, 114, 140, 164)
- `src/MessengerWebhook/Services/LiveCommentAutomationService.cs` (check for similar pattern)

## Implementation Steps

1. **Create private helper method** in `MessengerService.cs`:
   ```csharp
   private HttpRequestMessage CreateGraphRequest(HttpMethod method, string url, string? pageAccessToken = null)
   {
       var request = new HttpRequestMessage(method, url);
       var token = pageAccessToken ?? _globalPageAccessToken;
       request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
       return request;
   }
   ```

2. **Replace all `PostAsJsonAsync`/`GetAsync`/`PostAsync` calls** that build URLs with `?access_token={token}`:
   - Remove `?access_token=` from URL construction
   - Use `CreateGraphRequest()` to build requests
   - Send via `_httpClient.SendAsync()` instead of convenience methods
   - 5 locations total in MessengerService.cs

3. **Preserve backward compatibility**: The Facebook Graph API accepts both query string and Bearer header. After verification, remove query string path entirely.

## Success Criteria
- No `access_token` appears in any URL string
- All Graph API calls use Bearer Authorization header
- Existing tests pass (mocks should accept header-based auth)
- `dotnet build` succeeds

## Risk Assessment
- **Likelihood:** Low - Facebook API supports both auth methods
- **Impact:** Low - if something breaks, can quickly revert to query string
- **Mitigation:** Keep query string as fallback for 1 week, add log warning if fallback used

## Rollback
Revert commit. Query string auth still works per Facebook API docs.
