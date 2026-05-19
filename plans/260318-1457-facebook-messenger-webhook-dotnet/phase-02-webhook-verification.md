# Phase 2: Webhook Verification Endpoint

## Context Links
- [Facebook Messenger API](../reports/researcher-260318-1431-facebook-messenger-api.md) - Section: Webhook Verification Process

## Overview
- **Priority:** P0 (Critical)
- **Status:** Completed
- **Mô tả:** Implement GET /webhook endpoint để Facebook verify webhook subscription

## Key Insights
- Facebook gửi GET request với 3 query params: hub.mode, hub.verify_token, hub.challenge
- Phải verify token khớp với configured token
- Trả về hub.challenge nếu valid, 403 nếu invalid
- Endpoint chỉ được gọi khi setup webhook trong Facebook App Dashboard

## Requirements

**Functional:**
- Endpoint GET /webhook nhận query parameters
- Validate hub.mode === "subscribe"
- Validate hub.verify_token khớp với configured token
- Return hub.challenge với status 200 nếu valid
- Return 403 Forbidden nếu invalid

**Non-Functional:**
- Response time < 50ms
- Clear error logging
- Idempotent

## Architecture

**Request Flow:**
```
Facebook → GET /webhook?hub.mode=subscribe&hub.verify_token=XXX&hub.challenge=YYY
         ↓
    Validate mode === "subscribe"
         ↓
    Validate token === configured token
         ↓
    Return challenge (200) hoặc 403
```

## Related Code Files

**To Create:**
- `src/MessengerWebhook/Models/WebhookVerificationRequest.cs`

**To Modify:**
- `src/MessengerWebhook/Program.cs`

## Implementation Steps

1. **Tạo WebhookVerificationRequest model**
```csharp
public record WebhookVerificationRequest(
    string Mode,
    string VerifyToken,
    string Challenge
);
```

2. **Implement GET /webhook endpoint**
```csharp
app.MapGet("/webhook", (
    [FromQuery(Name = "hub.mode")] string? mode,
    [FromQuery(Name = "hub.verify_token")] string? verifyToken,
    [FromQuery(Name = "hub.challenge")] string? challenge,
    IOptions<WebhookOptions> options,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(verifyToken) || string.IsNullOrEmpty(challenge))
    {
        logger.LogWarning("Webhook verification failed: Missing parameters");
        return Results.BadRequest("Missing required parameters");
    }

    if (mode != "subscribe")
    {
        logger.LogWarning("Webhook verification failed: Invalid mode {Mode}", mode);
        return Results.Forbidden();
    }

    if (verifyToken != options.Value.VerifyToken)
    {
        logger.LogWarning("Webhook verification failed: Invalid verify token");
        return Results.Forbidden();
    }

    logger.LogInformation("Webhook verified successfully");
    return Results.Text(challenge);
});
```

3. **Add validation cho WebhookOptions**
- Ensure VerifyToken không null/empty khi app start

4. **Write unit tests**
- ValidParameters_ReturnsChallenge
- InvalidMode_ReturnsForbidden
- InvalidToken_ReturnsForbidden
- MissingParameters_ReturnsBadRequest

5. **Write integration test**
```csharp
[Fact]
public async Task GetWebhook_ValidRequest_Returns200WithChallenge()
{
    var client = _factory.CreateClient();
    var challenge = "test_challenge_123";

    var response = await client.GetAsync(
        $"/webhook?hub.mode=subscribe&hub.verify_token={validToken}&hub.challenge={challenge}");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().Be(challenge);
}
```

6. **Test manually**
```bash
curl "http://localhost:5000/webhook?hub.mode=subscribe&hub.verify_token=YOUR_TOKEN&hub.challenge=test123"
```

## Todo List
- [x] Tạo WebhookVerificationRequest record
- [x] Implement GET /webhook endpoint
- [x] Add validation cho WebhookOptions
- [x] Write 4 unit tests
- [x] Write integration test
- [x] Test manually với curl
- [x] Document endpoint trong README

## Success Criteria
- Valid params → 200 OK với challenge
- Invalid mode → 403
- Invalid token → 403
- Missing params → 400
- All tests pass
- Clear logging

## Risk Assessment
- **Risk:** Token mismatch do whitespace/encoding
  - **Mitigation:** Trim và validate token format
- **Risk:** Facebook thay đổi verification flow
  - **Mitigation:** Follow official docs, log attempts

## Security Considerations
- Verify token phải đủ dài và random (min 32 chars)
- Log failed attempts nhưng không log token values
- Rate limiting (implement sau)

## Next Steps
- Phase 3: POST /webhook endpoint
