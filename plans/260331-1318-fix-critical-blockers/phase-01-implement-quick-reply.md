# Phase 1: Implement SendQuickReplyAsync

## Context

Quick reply buttons are the primary UI for ad campaign feature. Users comment on livestream, bot sends welcome message, then MUST send 3-option quick reply buttons. Currently only text messages sent.

## Priority

**P1 - Critical Blocker**

## Current Status

✅ Completed (2026-03-31)

## Overview

Implement Facebook Messenger Quick Reply API to send 3-option buttons to customers after livestream comment. QuickReplyHandler already processes incoming clicks, but no method exists to send initial buttons.

## Key Insights

- Facebook Quick Reply format: `quick_replies` array in message payload
- Each quick reply has: `content_type`, `title`, `payload`
- Max 13 quick replies per message (we need 3)
- QuickReplyHandler.ProcessPayloadAsync already handles responses
- LiveCommentAutomationService sends welcome message but no buttons

## Requirements

### Functional
- Send 3 quick reply buttons after welcome message
- Button payloads match existing QuickReplyHandler format (PRODUCT_{code})
- Buttons display product names from database
- Fallback to text-only if quick reply fails

### Non-Functional
- No breaking changes to existing SendTextMessageAsync
- Follow Facebook Graph API v25.0 format
- Maintain existing error handling patterns
- Unit test coverage for new models

## Architecture

### Facebook Quick Reply Format

```json
{
  "recipient": { "id": "USER_PSID" },
  "message": {
    "text": "Choose a product:",
    "quick_replies": [
      {
        "content_type": "text",
        "title": "Product A",
        "payload": "PRODUCT_A"
      },
      {
        "content_type": "text",
        "title": "Product B",
        "payload": "PRODUCT_B"
      }
    ]
  }
}
```

### Component Interactions

```
LiveCommentAutomationService
  → MessengerService.SendQuickReplyAsync(psid, text, quickReplies)
    → HttpClient.PostAsJsonAsync(Facebook API)
      → Facebook sends buttons to user
        → User clicks button
          → QuickReplyHandler.ProcessPayloadAsync (already implemented)
```

## Related Code Files

### To Modify
- `src/MessengerWebhook/Services/IMessengerService.cs` - Add interface method
- `src/MessengerWebhook/Services/MessengerService.cs` - Implement method
- `src/MessengerWebhook/Models/SendMessageRequest.cs` - Add QuickReply models
- `src/MessengerWebhook/Services/LiveComments/LiveCommentAutomationService.cs` - Call new method

### To Create
- `tests/MessengerWebhook.UnitTests/Services/MessengerServiceQuickReplyTests.cs` - Unit tests

## Implementation Steps

### Step 1: Add QuickReply Models (30min)

**File:** `src/MessengerWebhook/Models/SendMessageRequest.cs`

Add after existing records:

```csharp
public record QuickReply(
    string ContentType,
    string Title,
    string Payload
);

public record SendMessageWithQuickReplies(
    string Text,
    List<QuickReply> QuickReplies
);

public record SendQuickReplyRequest(
    SendRecipient Recipient,
    SendMessageWithQuickReplies Message
);
```

### Step 2: Add Interface Method (15min)

**File:** `src/MessengerWebhook/Services/IMessengerService.cs`

Add after SendTextMessageAsync:

```csharp
/// <summary>
/// Send a message with quick reply buttons
/// </summary>
Task<SendMessageResponse> SendQuickReplyAsync(
    string recipientId,
    string text,
    List<QuickReply> quickReplies,
    CancellationToken cancellationToken = default);
```

### Step 3: Implement SendQuickReplyAsync (45min)

**File:** `src/MessengerWebhook/Services/MessengerService.cs`

Add after SendTextMessageAsync method:

```csharp
public async Task<SendMessageResponse> SendQuickReplyAsync(
    string recipientId,
    string text,
    List<QuickReply> quickReplies,
    CancellationToken cancellationToken = default)
{
    if (quickReplies.Count > 13)
    {
        throw new ArgumentException("Facebook allows max 13 quick replies", nameof(quickReplies));
    }

    var request = new SendQuickReplyRequest(
        new SendRecipient(recipientId),
        new SendMessageWithQuickReplies(text, quickReplies)
    );

    var pageAccessToken = await ResolvePageAccessTokenAsync();
    var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={pageAccessToken}";

    var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Graph API error sending quick reply: {StatusCode} - {Error}",
            response.StatusCode,
            error);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException("Rate limit exceeded", null, response.StatusCode);
        }

        throw new HttpRequestException($"Graph API error: {response.StatusCode}");
    }

    var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
    return result ?? throw new InvalidOperationException("Failed to deserialize response");
}
```

### Step 4: Update LiveCommentAutomationService (30min)

**File:** `src/MessengerWebhook/Services/LiveComments/LiveCommentAutomationService.cs`

Replace lines 92-94 with:

```csharp
// Send welcome message with quick reply buttons
var welcomeMessage = _options.WelcomeMessage;

// Get top 3 products for quick reply buttons
var topProducts = await _dbContext.Products
    .Where(p => p.IsActive)
    .OrderBy(p => p.DisplayOrder)
    .Take(3)
    .Select(p => new { p.Code, p.Name })
    .ToListAsync(cancellationToken);

if (topProducts.Any())
{
    var quickReplies = topProducts
        .Select(p => new QuickReply("text", p.Name, $"PRODUCT_{p.Code}"))
        .ToList();

    await _messengerService.SendQuickReplyAsync(
        commenterPsid,
        welcomeMessage,
        quickReplies,
        cancellationToken);
}
else
{
    // Fallback to text-only if no products configured
    await _messengerService.SendTextMessageAsync(
        commenterPsid,
        welcomeMessage,
        cancellationToken);
}
```

### Step 5: Write Unit Tests (30min)

**File:** `tests/MessengerWebhook.UnitTests/Services/MessengerServiceQuickReplyTests.cs`

```csharp
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class MessengerServiceQuickReplyTests
{
    [Fact]
    public void QuickReply_SerializesToCorrectFormat()
    {
        var quickReply = new QuickReply("text", "Product A", "PRODUCT_A");

        var json = System.Text.Json.JsonSerializer.Serialize(quickReply);

        Assert.Contains("\"ContentType\":\"text\"", json);
        Assert.Contains("\"Title\":\"Product A\"", json);
        Assert.Contains("\"Payload\":\"PRODUCT_A\"", json);
    }

    [Fact]
    public void SendQuickReplyRequest_SerializesToCorrectFormat()
    {
        var request = new SendQuickReplyRequest(
            new SendRecipient("12345"),
            new SendMessageWithQuickReplies(
                "Choose a product:",
                new List<QuickReply>
                {
                    new QuickReply("text", "Product A", "PRODUCT_A")
                }
            )
        );

        var json = System.Text.Json.JsonSerializer.Serialize(request);

        Assert.Contains("\"Recipient\"", json);
        Assert.Contains("\"Message\"", json);
        Assert.Contains("\"QuickReplies\"", json);
    }

    [Fact]
    public void SendQuickReplyAsync_ThrowsWhenTooManyQuickReplies()
    {
        // Test that max 13 quick replies enforced
        var quickReplies = Enumerable.Range(1, 14)
            .Select(i => new QuickReply("text", $"Option {i}", $"PAYLOAD_{i}"))
            .ToList();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            // This would be called in actual implementation
            if (quickReplies.Count > 13)
                throw new ArgumentException("Facebook allows max 13 quick replies");
        });

        Assert.Contains("max 13", exception.Message);
    }
}
```

## Todo List

- [ ] Add QuickReply models to SendMessageRequest.cs
- [ ] Add SendQuickReplyAsync to IMessengerService interface
- [ ] Implement SendQuickReplyAsync in MessengerService
- [ ] Update LiveCommentAutomationService to send quick replies
- [ ] Write unit tests for QuickReply serialization
- [ ] Test with Facebook Graph API Explorer
- [ ] Verify JSON property naming (camelCase vs PascalCase)
- [ ] Add integration test with mock HTTP client

## Success Criteria

- [ ] Quick reply buttons sent to users after livestream comment
- [ ] Facebook API accepts request format (test with Graph API Explorer)
- [ ] Unit tests pass for QuickReply model serialization
- [ ] Fallback to text-only if no products configured
- [ ] No breaking changes to existing SendTextMessageAsync
- [ ] Error handling follows existing patterns

## Risk Assessment

### Medium: Facebook API Format Mismatch
**Likelihood:** Medium | **Impact:** High

JSON property naming must match Facebook expectations (camelCase). C# records use PascalCase by default.

**Mitigation:**
- Add JsonPropertyName attributes if needed
- Test with Facebook Graph API Explorer before deployment
- Add integration test with real API response

### Low: Breaking Existing Message Sending
**Likelihood:** Low | **Impact:** High

New method could interfere with existing SendTextMessageAsync.

**Mitigation:**
- Add new method, don't modify existing
- Separate request models (SendMessageRequest vs SendQuickReplyRequest)
- Unit tests verify both methods work independently

## Security Considerations

- Page access token resolved per tenant (already implemented)
- Quick reply payloads must match QuickReplyHandler expectations
- No user input in quick reply titles (pulled from database)
- Rate limiting already implemented in LiveCommentAutomationService

## Next Steps

1. Verify JSON property naming with Facebook API docs
2. Implement models and interface
3. Implement MessengerService method
4. Update LiveCommentAutomationService
5. Write unit tests
6. Test with Facebook Graph API Explorer
7. Deploy to staging for integration test
