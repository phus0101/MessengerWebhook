---
phase: 03
title: "H1: PII Log Redaction"
priority: P2 (High)
status: pending
depends_on: none
---

## Overview
Mask PII (phone numbers, addresses) in application logs to comply with privacy regulations.

## Files to Modify
- `src/MessengerWebhook/Services/SalesStateHandlerBase.cs` (lines ~110)
- `src/MessengerWebhook/Services/SalesMessageParser.cs` (line ~42)
- `src/MessengerWebhook/Utilities/PiiRedaction.cs` (new)

## Implementation Steps

1. **Create `PiiRedaction` utility class**
   - File: `src/MessengerWebhook/Utilities/PiiRedaction.cs`
   - Methods:
     ```csharp
     public static string MaskPhone(string phone) // "0912****5678"
     public static string MaskAddress(string address) // "123 **** Hanoi"
     public static object RedactTemplate(string template, params object[] args)
     ```
   - Compiled regex for Vietnamese phone pattern: `\b(0\d{2})\d{4}(\d{3})\b`

2. **Update log statements in `SalesStateHandlerBase.cs`**
   - Replace: `_logger.LogInformation("Loaded remembered phone for PSID: {PSID}: {Phone}", psid, phone);`
   - With: `_logger.LogInformation("Loaded remembered phone for PSID: {PSID}: {Phone}", psid, PiiRedaction.MaskPhone(phone));`
   - Apply to all 8+ log statements containing phone/address

3. **Update log statements in `SalesMessageParser.cs`**
   - Same pattern for AI-extracted phone/address logs

4. **Add structured logging support**
   - Use Serilog enrichment pipeline to auto-redact PII in log properties
   - Add `Destructurama.ByIgnoring` for known PII-containing types

## Success Criteria
- No plaintext phone numbers or addresses in log output
- Masked values still useful for debugging (first/last 4 digits visible)
- All existing tests pass
- `dotnet build` succeeds

## Risk Assessment
- **Likelihood:** Low
- **Impact:** Low if regex fails (fallback to full mask: `****`)
- **Mitigation:** Unit tests for redaction with edge cases

## Rollback
Revert commit. Redaction is additive, safe to remove temporarily.
