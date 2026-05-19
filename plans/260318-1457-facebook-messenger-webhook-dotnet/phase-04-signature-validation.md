# Phase 4: Signature Validation

## Context Links
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md) - Section: Request Validation & Signature Verification

## Overview
- **Priority:** P0 (Critical - Security)
- **Status:** Pending
- **Mô tả:** Implement HMAC-SHA256 signature validation để verify requests từ Facebook

## Key Insights
- Facebook gửi X-Hub-Signature-256 header với format "sha256=<hash>"
- Hash được tính từ raw request body với App Secret
- Dùng constant-time comparison để prevent timing attacks
- Signature validation phải chạy trước JSON parsing

## Requirements

**Functional:**
- Read raw request body trước deserialization
- Extract X-Hub-Signature-256 header
- Compute HMAC-SHA256 với App Secret
- Compare với constant-time algorithm
- Reject request nếu signature không khớp

**Non-Functional:**
- Validation time < 10ms
- Zero false positives/negatives
- Secure against timing attacks

## Architecture

**Validation Flow:**
```
Request → Middleware
        ↓
    Read raw body
        ↓
    Extract X-Hub-Signature-256
        ↓
    Compute HMAC-SHA256(body, appSecret)
        ↓
    CryptographicOperations.FixedTimeEquals()
        ↓
    Valid → Continue | Invalid → 401
```

## Related Code Files

**To Create:**
- `src/MessengerWebhook/Services/ISignatureValidator.cs`
- `src/MessengerWebhook/Services/SignatureValidator.cs`
- `src/MessengerWebhook/Middleware/SignatureValidationMiddleware.cs`

**To Modify:**
- `src/MessengerWebhook/Program.cs`
- `src/MessengerWebhook/Configuration/FacebookOptions.cs`

## Implementation Steps

1. **Tạo ISignatureValidator interface**
```csharp
public interface ISignatureValidator
{
    Task<bool> ValidateAsync(string rawBody, string signature);
}
```

2. **Implement SignatureValidator**
```csharp
public class SignatureValidator : ISignatureValidator
{
    private readonly string _appSecret;

    public Task<bool> ValidateAsync(string rawBody, string signature)
    {
        if (!signature.StartsWith("sha256="))
            return Task.FromResult(false);

        var providedHash = signature.Substring(7);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedHashString = BitConverter.ToString(computedHash)
            .Replace("-", "").ToLower();

        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedHash),
            Encoding.UTF8.GetBytes(computedHashString));

        return Task.FromResult(isValid);
    }
}
```

3. **Implement SignatureValidationMiddleware**
```csharp
public class SignatureValidationMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "POST" && context.Request.Path == "/webhook")
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var signature = context.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing signature");
                return;
            }

            if (!await _validator.ValidateAsync(rawBody, signature))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid signature");
                return;
            }
        }

        await _next(context);
    }
}
```

4. **Register services**
```csharp
builder.Services.AddSingleton<ISignatureValidator, SignatureValidator>();
app.UseMiddleware<SignatureValidationMiddleware>();
```

5. **Write unit tests**
- ValidSignature_ReturnsTrue
- InvalidSignature_ReturnsFalse
- MissingSignature_ReturnsFalse
- WrongFormat_ReturnsFalse

6. **Write integration test**
```csharp
[Fact]
public async Task PostWebhook_ValidSignature_Returns200()
{
    var payload = "{\"object\":\"page\"}";
    var signature = ComputeSignature(payload, appSecret);

    var request = new HttpRequestMessage(HttpMethod.Post, "/webhook")
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("X-Hub-Signature-256", signature);

    var response = await client.SendAsync(request);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## Todo List
- [ ] Tạo ISignatureValidator interface
- [ ] Implement SignatureValidator
- [ ] Implement SignatureValidationMiddleware
- [ ] Register services và middleware
- [ ] Write 4 unit tests
- [ ] Write integration test với valid/invalid signatures
- [ ] Test với real Facebook signature

## Success Criteria
- Valid signature → request processed
- Invalid signature → 401 Unauthorized
- Missing signature → 401 Unauthorized
- Constant-time comparison used
- All tests pass
- Validation time < 10ms

## Risk Assessment
- **Risk:** Timing attack vulnerability
  - **Mitigation:** CryptographicOperations.FixedTimeEquals()
- **Risk:** Raw body read twice
  - **Mitigation:** EnableBuffering() và reset Position

## Security Considerations
- CRITICAL: Dùng constant-time comparison
- Store App Secret securely (User Secrets/Env Vars)
- Log validation failures nhưng không log secrets
- Consider IP whitelisting (Facebook IP ranges)

## Next Steps
- Phase 5: Async processing với BackgroundService
