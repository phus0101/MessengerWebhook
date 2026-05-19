# Phase 6: PII Log Redaction (H1)

## Overview
- Priority: High
- Current status: Not started (depends on Phase 0 — no hard dependency, can run in parallel)
- Effort: 1h
- Issue: H1 — PII (phone, address) leakage in logs

## Problem
Customer phone numbers and addresses logged in plaintext:
- `SalesStateHandlerBase.cs:110` — "Loaded remembered phone for PSID: ...: <phone>"
- `SalesStateHandlerBase.cs:117` — "Loaded remembered address for PSID: ...: <address>"
- `SalesMessageParser.cs:42` — "AI extracted phone from message for PSID ...: <phone>"
- Multiple other locations with phone/address in log messages

## Context Links
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`
- `src/MessengerWebhook/Services/MessengerService.cs` (potential PII in error logs)

## Architecture

Create a `PiiRedactor` utility that masks sensitive data:

```csharp
public static class PiiRedactor
{
    // "0912345678" -> "0912***678"
    public static string RedactPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? phone : MaskMiddle(phone, 3, 3);

    // "123 Street, City" -> "*** Street, ***"
    public static string RedactAddress(string? address) =>
        string.IsNullOrWhiteSpace(address) ? address : MaskMiddle(address, 3, 3);

    private static string MaskMiddle(string value, int keepStart, int keepEnd)
    {
        if (value.Length <= keepStart + keepEnd) return "***";
        return value[..keepStart] + "***" + value[^keepEnd..];
    }
}
```

All log statements with PII go through redaction:
```csharp
// Before
logger.LogInformation("Loaded remembered phone: {Phone}", customer.PhoneNumber);
// After
logger.LogInformation("Loaded remembered phone: {Phone}", PiiRedactor.RedactPhone(customer.PhoneNumber));
```

Alternative: Serilog enrichment approach — add a custom enricher that runs regex to mask phone/address patterns. More invasive and riskier. The explicit `PiiRedactor` approach is safer and self-documenting.

## Implementation Steps

### Step 1: Create PiiRedactor.cs

Create `src/MessengerWebhook/Utilities/PiiRedactor.cs` with:
- `RedactPhone(string?)` — mask middle digits, keep first 3 and last 3
- `RedactAddress(string?)` — mask middle, show first 3 and last 3 chars
- `RedactEmail(string?)` — mask username, keep domain

### Step 2: Update log statements

In `SalesStateHandlerBase.cs`, redact all log statements containing:
- phone numbers
- shipping addresses

In `SalesMessageParser.cs`, redact all log statements containing:
- extracted phone/address
- confirmation detection messages

### Step 3: Add Serilog enricher (optional, defense-in-depth)

Add to Serilog config to catch PII that wasn't explicitly redacted:
```csharp
.Enrich.With(new PiiRedactionEnricher()) // Regex-based catch-all
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Utilities/PiiRedactor.cs`

**To modify:**
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (redact PII in logs)
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs` (redact PII in logs)
- `Program.cs` (Serilog config, optional enricher)

## Todo List

- [ ] Create PiiRedactor.cs
- [ ] Update SalesStateHandlerBase.cs log redaction
- [ ] Update SalesMessageParser.cs log redaction
- [ ] Run dotnet build

## Success Criteria

- No plaintext phone numbers or addresses in log output
- Redacted format: "0912***678" for phone, "123***Str" for address
- `dotnet build` succeeds

## Risk Assessment

**Low risk.** Pure logging change, no behavioral impact. Tests that assert log messages will need updating to expect redacted format.
