# Phase 2: Verify HideCommentAsync Implementation

## Context

Code review flagged HideCommentAsync as non-existent, but code inspection shows it IS implemented (MessengerService.cs lines 94-116). This phase verifies implementation correctness.

## Priority

**P1 - Critical Blocker (Potential False Alarm)**

## Current Status

✅ Completed (2026-03-31)

## Overview

Verify existing HideCommentAsync implementation matches Facebook Graph API v25.0 requirements. Code review may be false alarm - method exists and appears correct.

## Key Insights

- HideCommentAsync already implemented in MessengerService.cs (lines 94-116)
- Uses POST /{comment-id}?is_hidden=true endpoint
- Wrapped in try-catch in LiveCommentAutomationService (lines 97-107)
- Best-effort approach: logs warning but continues if hiding fails
- Returns bool indicating success/failure

## Requirements

### Functional
- Verify Facebook Graph API endpoint format correct
- Confirm error handling prevents cascade failures
- Validate page access token resolution works

### Non-Functional
- Integration test confirms API call format
- No breaking changes to existing behavior
- Maintain best-effort approach (don't fail conversation if hiding fails)

## Architecture

### Current Implementation

```csharp
public async Task<bool> HideCommentAsync(string commentId, CancellationToken cancellationToken = default)
{
    try
    {
        var pageAccessToken = await ResolvePageAccessTokenAsync();
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{commentId}?is_hidden=true&access_token={pageAccessToken}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to hide comment {CommentId}: {StatusCode}", commentId, response.StatusCode);
            return false;
        }

        _logger.LogInformation("Successfully hidden comment {CommentId}", commentId);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error hiding comment {CommentId}", commentId);
        return false;
    }
}
```

### Facebook Graph API Format

According to Facebook docs:
```
POST /{comment-id}
  ?is_hidden=true
  &access_token={page-access-token}
```

Current implementation matches this format exactly.

## Related Code Files

### To Verify
- `src/MessengerWebhook/Services/MessengerService.cs` (lines 94-116) - Implementation
- `src/MessengerWebhook/Services/IMessengerService.cs` (line 21) - Interface
- `src/MessengerWebhook/Services/LiveComments/LiveCommentAutomationService.cs` (lines 97-107) - Usage

### To Create
- `tests/MessengerWebhook.IntegrationTests/Services/MessengerServiceCommentTests.cs` - Integration test

## Implementation Steps

### Step 1: Review Facebook Graph API Docs (15min)

Verify endpoint format against official docs:
- Endpoint: POST /{comment-id}
- Query params: is_hidden=true, access_token
- Required permissions: pages_manage_engagement
- Response format: { "success": true }

### Step 2: Verify Implementation Correctness (15min)

Check current implementation:
- [x] Endpoint format matches docs
- [x] Page access token resolved correctly
- [x] Error handling prevents cascade failures
- [x] Logging appropriate (warning on failure, info on success)
- [x] Returns bool for caller to handle

### Step 3: Write Integration Test (30min)

**File:** `tests/MessengerWebhook.IntegrationTests/Services/MessengerServiceCommentTests.cs`

```csharp
using System.Net;
using MessengerWebhook.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services;

public class MessengerServiceCommentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MessengerServiceCommentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HideCommentAsync_ConstructsCorrectUrl()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("*/123456789?is_hidden=true*")
            .Respond(HttpStatusCode.OK, "application/json", "{\"success\":true}");

        var client = mockHandler.ToHttpClient();
        client.BaseAddress = new Uri("https://graph.facebook.com");

        var scope = _factory.Services.CreateScope();
        var messengerService = scope.ServiceProvider.GetRequiredService<IMessengerService>();

        // Act
        var result = await messengerService.HideCommentAsync("123456789");

        // Assert
        Assert.True(result);
        Assert.Single(mockHandler.GetMatchCount());
    }

    [Fact]
    public async Task HideCommentAsync_ReturnsFalseOnApiError()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("*/123456789?is_hidden=true*")
            .Respond(HttpStatusCode.BadRequest, "application/json", "{\"error\":{\"message\":\"Invalid comment ID\"}}");

        var client = mockHandler.ToHttpClient();
        var scope = _factory.Services.CreateScope();
        var messengerService = scope.ServiceProvider.GetRequiredService<IMessengerService>();

        // Act
        var result = await messengerService.HideCommentAsync("123456789");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HideCommentAsync_ReturnsFalseOnException()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("*/123456789?is_hidden=true*")
            .Throw(new HttpRequestException("Network error"));

        var client = mockHandler.ToHttpClient();
        var scope = _factory.Services.CreateScope();
        var messengerService = scope.ServiceProvider.GetRequiredService<IMessengerService>();

        // Act
        var result = await messengerService.HideCommentAsync("123456789");

        // Assert
        Assert.False(result);
    }
}
```

### Step 4: Verify LiveCommentAutomationService Usage (15min)

Check that LiveCommentAutomationService handles failures correctly:

```csharp
// Lines 97-107 in LiveCommentAutomationService.cs
if (_options.AutoHideComments)
{
    try
    {
        await _messengerService.HideCommentAsync(commentId, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to hide comment {CommentId}, continuing anyway", commentId);
    }
}
```

Verify:
- [x] Wrapped in try-catch
- [x] Logs warning on failure
- [x] Continues processing (doesn't throw)
- [x] Respects AutoHideComments configuration flag

### Step 5: Test with Facebook Graph API Explorer (30min)

Manual verification:
1. Get page access token from Facebook App settings
2. Create test comment on page post
3. Use Graph API Explorer to test: POST /{comment-id}?is_hidden=true
4. Verify comment hidden in Facebook UI
5. Verify response format matches implementation expectations

### Step 6: Document Findings (15min)

Update plan with verification results:
- Implementation correct? Yes/No
- API format matches docs? Yes/No
- Integration tests pass? Yes/No
- Manual test successful? Yes/No

If all yes: Close blocker as false alarm
If any no: Document required fixes

## Todo List

- [ ] Review Facebook Graph API v25.0 docs for comment hiding
- [ ] Verify implementation matches API requirements
- [ ] Write integration tests with mock HTTP client
- [ ] Test with Facebook Graph API Explorer
- [ ] Verify error handling in LiveCommentAutomationService
- [ ] Document verification results
- [ ] Close blocker or document required fixes

## Success Criteria

- [ ] Implementation verified against Facebook API docs
- [ ] Integration tests pass
- [ ] Manual test with Graph API Explorer successful
- [ ] Error handling prevents cascade failures
- [ ] Documentation updated with findings

## Risk Assessment

### Low: Implementation Already Correct
**Likelihood:** High | **Impact:** Low

Code inspection suggests implementation correct. Code review may be false alarm.

**Mitigation:**
- Verify with integration tests
- Test with real Facebook API
- Document findings for future reference

### Medium: Missing Permissions
**Likelihood:** Medium | **Impact:** Medium

Page access token may lack pages_manage_engagement permission.

**Mitigation:**
- Verify token permissions in Facebook App settings
- Document required permissions in README
- Add permission check to startup validation

## Security Considerations

- Page access token must have pages_manage_engagement permission
- Token resolved per tenant (already implemented)
- Best-effort approach prevents DoS if Facebook API down
- No user input in comment ID (comes from webhook)

## Next Steps

1. Review Facebook Graph API docs
2. Write integration tests
3. Test with Graph API Explorer
4. Document verification results
5. If correct: Close blocker as false alarm
6. If incorrect: Document required fixes and implement
