# Phase 3: Move Access Token to Bearer Header (C4)

## Overview
- Priority: Critical
- Current status: Not started
- Effort: 30min
- Issue: C4 — Access token exposed in query string, leaks to proxy/CDN logs

## Problem
`MessengerService` passes `access_token` as query parameter in all Graph API calls. Query strings are logged by proxies, CDNs, APM tools, and load balancers → token exposure.

Current pattern in 5 locations:
```csharp
var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={pageAccessToken}";
```

## Context Links
- `src/MessengerWebhook/Services/MessengerService.cs` (lines 44, 85, 114, 140, 164)

## Architecture

Replace query string with `Authorization: Bearer` header:

```csharp
// Before
var url = "https://graph.facebook.com/v21.0/me/messages?access_token=TOKEN";
await _httpClient.PostAsJsonAsync(url, request);

// After
var url = "https://graph.facebook.com/v21.0/me/messages";
using var request = new HttpRequestMessage(HttpMethod.Post, url);
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);
request.Content = JsonContent.Create(body);
await _httpClient.SendAsync(request);
```

Facebook Graph API officially supports both methods; Bearer is the OAuth 2.0 standard and preferred.

## Implementation Steps

### Step 1: Create helper method in MessengerService

Add private method:
```csharp
private HttpRequestMessage CreateGraphRequest(HttpMethod method, string endpoint)
{
    var request = new HttpRequestMessage(method, $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{endpoint}");
    var token = ResolvePageAccessTokenAsync().Result; // Synchronous, called from sync path
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return request;
}
```

Better: make it async and use `async` everywhere:
```csharp
private async Task<HttpRequestMessage> CreateGraphRequestAsync(HttpMethod method, string path)
{
    var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{path}";
    var request = new HttpRequestMessage(method, url);
    var token = await ResolvePageAccessTokenAsync();
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return request;
}
```

### Step 2: Update all 5 call sites

- `SendTextMessageAsync` — POST /me/messages
- `SendQuickReplyAsync` — POST /me/messages
- `IsVideoLiveAsync` — GET /{videoId}
- `HideCommentAsync` — POST /{commentId}
- `ReplyToCommentAsync` — POST /{commentId}/replies

Each changes from `PostAsJsonAsync(url, body)` to `SendAsync(await CreateGraphRequestAsync(...))`.

## Related Code Files

**To modify:**
- `src/MessengerWebhook/Services/MessengerService.cs` (all 5 Graph API call sites)

## Todo List

- [ ] Create CreateGraphRequestAsync helper method
- [ ] Update SendTextMessageAsync
- [ ] Update SendQuickReplyAsync
- [ ] Update IsVideoLiveAsync
- [ ] Update HideCommentAsync
- [ ] Update ReplyToCommentAsync
- [ ] Run dotnet build
- [ ] Run unit tests for MessengerService

## Success Criteria

- No `access_token` in any URL
- All Graph API calls use Bearer Authorization header
- All existing tests pass
- `dotnet build` succeeds

## Risk Assessment

**Low-Medium risk.** Facebook Graph API supports Bearer tokens natively. Main risk is subtle differences in how error responses are handled — but the response parsing should be identical since we only changed how auth is sent, not the URL or body.
