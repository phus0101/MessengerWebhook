# Phase 8: Low-Priority Fixes + Compilation (L1-L5)

## Overview
- Priority: Low
- Current status: Not started
- Effort: 1h
- Issues: L1 (Grpc compatibility), L2 (CS8602 in tests), L3 (xUnit1012), L4 (hardcoded Vietnamese strings), L5 (large snapshot)

## L1: Grpc.Net.ClientFactory Compatibility Warning
Build warning about Grpc client factory compatibility with .NET 8. If the warning persists, pin to compatible version or remove if unused.

- Check `src/MessengerWebhook/MessengerWebhook.csproj` for Grpc references
- If unused, remove dependency
- If used, update to latest compatible version

---

## L2: CS8602 Null Reference Warnings in Tests
Multiple test files have `CS8602` (dereference of possibly null reference). Fix by:
- Adding `!` null-forgiving operator where null is known safe
- Or adding null checks where appropriate

- `tests/MessengerWebhook.IntegrationTests/` (various)

---

## L3: xUnit1012 Null Pattern in Test
`WebhookEventDeserializationTests.cs` uses null pattern that xUnit warns about.

Fix: Replace `[InlineData(null)]` with `[MemberData]` or explicit null handling per xUnit best practices.

---

## L4: Hardcoded Vietnamese UI Strings
Vietnamese UI strings scattered throughout `SalesStateHandlerBase.cs` and `SalesMessageParser.cs`.

For this phase: **Do NOT convert to .resx** (YAGNI). Instead, extract to `const` fields or `static readonly` strings for centralization:

```csharp
internal static class SalesBotMessages
{
    public const string TechnicalError = "Dạ em đang gặp sự cố kỹ thuật. Chị nhắn lại giúp em sau ít phút nha.";
    public const string NoProductReply = "Dạ em chưa rõ chị muốn đặt sản phẩm nào ạ. Chị cho em biết tên sản phẩm để em lên đơn nhé.";
}
```

---

## L5: DbContextModelSnapshot.cs (1,571 lines)
Generated file. Consider consolidating migrations but this is not critical.

Action: Document decision and defer. Note in `development-roadmap.md` for future consideration.

---

## Implementation Steps

### Step 1: Fix CS8602 warnings in test files
### Step 2: Fix xUnit1012 in WebhookEventDeserializationTests
### Step 3: Extract hardcoded Vietnamese strings to constants
### Step 4: Verify Grpc dependency
### Step 5: Run full build and test suite

## Related Code Files

**To create:**
- `src/MessengerWebhook/Utilities/SalesBotMessages.cs` (extracted Vietnamese constants)

**To modify:**
- Test files with CS8602 warnings
- `tests/MessengerWebhook.UnitTests/WebhookEventDeserializationTests.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (use constants)
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs` (use constants)
- `src/MessengerWebhook/MessengerWebhook.csproj` (Grpc fix if needed)

## Todo List

- [ ] L1: Review and fix Grpc dependency
- [ ] L2: Fix CS8602 warnings in test files
- [ ] L3: Fix xUnit1012 warning
- [ ] L4: Extract Vietnamese strings to SalesBotMessages.cs
- [ ] L5: Document migration consolidation decision
- [ ] Run `dotnet build` — verify 0 errors
- [ ] Run `dotnet test` — verify all tests pass

## Success Criteria

- `dotnet build` succeeds with 0 errors and minimal warnings
- `dotnet test` passes all unit and integration tests
- No hardcoded Vietnamese strings in handler logic
- Grpc warning resolved or documented as acceptable

## Risk Assessment

**Very low risk.** These are polish changes — test fixes, string extraction, warning cleanup. No behavioral changes.
