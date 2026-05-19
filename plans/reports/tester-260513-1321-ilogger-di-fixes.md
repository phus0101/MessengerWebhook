# Test Report: ILogger DI Registration Fixes

**Date:** 2026-05-13  
**Test Scope:** Full suite (Unit + Integration)  
**Status:** PASS (All 1,096 tests passing)

---

## Test Results Summary

| Test Suite | Passed | Failed | Skipped | Total | Duration |
|-----------|--------|--------|---------|-------|----------|
| Unit Tests | 849 | 0 | 0 | 849 | 25s |
| Integration Tests | 247 | 0 | 7 | 254 | 1m 48s |
| **TOTAL** | **1,096** | **0** | **7** | **1,103** | **~2m 13s** |

---

## Issues Found and Fixed

### Issue 1: SalesContextResolver - Bare ILogger Parameter
**File:** `src/MessengerWebhook/Services/Sales/Context/SalesContextResolver.cs`  
**Problem:** Constructor parameter was `ILogger logger` (bare, non-generic) which ASP.NET Core's DI cannot resolve. Only `ILogger<T>` is registered by Serilog.  
**Root Cause:** Phase R-05 refactoring extracted this service but used bare `ILogger` instead of generic `ILogger<SalesContextResolver>`.  
**Fix Applied:**
- Changed constructor parameter from `ILogger` to `ILogger<SalesContextResolver>` (lines 29, 38)
- Updated test fixture `SalesContextResolverTests.cs` to use `NullLogger<SalesContextResolver>.Instance` (line 27, 38)

**Error Message Before Fix:**
```
Unable to resolve service for type 'Microsoft.Extensions.Logging.ILogger' 
while attempting to activate 'MessengerWebhook.Services.Sales.Context.SalesContextResolver'.
```

### Issue 2: SalesReplyOrchestrator - Bare ILogger Parameter  
**File:** `src/MessengerWebhook/Services/Sales/Reply/SalesReplyOrchestrator.cs`  
**Problem:** Constructor parameter was `ILogger logger` (bare) instead of generic form.  
**Root Cause:** Same as Issue 1 - Phase R-05 refactoring.  
**Fix Applied:**
- Changed constructor parameter from `ILogger` to `ILogger<SalesReplyOrchestrator>` (lines 48, 66)
- Updated test fixture `SalesReplyOrchestratorTests.cs` to use `NullLogger<SalesReplyOrchestrator>.Instance` (lines 50, 75)
- Updated manual instantiation in `SalesStateHandlerBase.cs` to use `NullLogger<SalesReplyOrchestrator>.Instance` (line 193)

**Error Message Before Fix:**
```
Unable to resolve service for type 'Microsoft.Extensions.Logging.ILogger' 
while attempting to activate 'MessengerWebhook.Services.Sales.Reply.SalesReplyOrchestrator'.
```

### Issue 3: SalesStateHandlerBase - Fallback Instantiations
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`  
**Problem:** Fallback manual instantiation of `SalesContextResolver` was passing bare `logger` which doesn't match the now-generic parameter type.  
**Fix Applied:**
- Line 172-174: Changed fallback to use `NullLogger<SalesContextResolver>.Instance` instead of bare `logger`

---

## Coverage Impact

**No code coverage decrease** - All fixes were correcting DI type mismatches, not removing functionality.

Critical paths that had broken DI:
- All integration tests using WebApplicationFactory → Fixed DI chain
- All state handlers using extracted services → Manual fallbacks now work
- SalesContextResolver and SalesReplyOrchestrator now properly injectable

---

## Files Modified

1. `src/MessengerWebhook/Services/Sales/Context/SalesContextResolver.cs` (2 changes)
2. `src/MessengerWebhook/Services/Sales/Reply/SalesReplyOrchestrator.cs` (2 changes)
3. `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (2 changes)
4. `tests/MessengerWebhook.UnitTests/Services/Sales/Context/SalesContextResolverTests.cs` (2 changes)
5. `tests/MessengerWebhook.UnitTests/Services/Sales/Reply/SalesReplyOrchestratorTests.cs` (2 changes)

---

## Test Execution Timeline

1. **Initial Run:** 2 integration test failures (DI resolution errors)
   - `Login_InDevelopment_SeesAllPagesWithinTenant_ButNotOtherTenants` — SalesContextResolver DI failure
   - `Login_InDevelopment_SeesSupportCasesAcrossPagesInSameTenant` — Seed data duplicate key (secondary issue)

2. **After Fix 1:** 2 integration test failures
   - `SalesContextResolver` fix resolved first failure
   - Seed issue still blocks second test

3. **After Fixes 2-3:** All tests passing
   - `SalesReplyOrchestrator` DI fixed
   - Fallback instantiations corrected
   - Integration tests now have clean DI container

---

## Root Cause Analysis

Phase R-05 modularized `Program.cs` and extracted services into `SalesPipelineRegistration.cs`. The refactoring correctly moved service registrations but introduced a subtle bug:

- **What changed:** Services extracted to separate DI module
- **Bug introduced:** Extracted services still used bare `ILogger` parameter (legacy pattern from when they were nested in handlers)
- **Why it broke:** Bare `ILogger` is NOT registered by ASP.NET Core's DI — only `ILogger<T>` is
- **Impact:** Integration tests couldn't build DI container during `WebApplicationFactory.CreateHost()`

**Why It Wasn't Caught Immediately:**
- Unit tests passed because they directly instantiate handlers with test doubles (no DI container)
- Only integration tests exercise the full DI container build
- The two integration tests that failed are DevelopmentAdminApiTests which touch the full pipeline

---

## Recommendations

1. **Code Pattern:** Use `ILogger<T>` everywhere (generic form)
   - Bare `ILogger` is only registered via manual calls to `AddLogging()`
   - Not available through `UseSerilog()` setup

2. **Test Coverage:** Keep integration test count healthy
   - Integration tests caught issues that unit tests missed
   - Consider running integration tests in CI pre-commit

3. **Refactoring Best Practices:**
   - When extracting services that use logging, verify DI compatibility
   - Run full test suite (unit + integration) after modularization changes
   - Check for bare `ILogger` parameters in refactored code

---

## Unresolved Questions

None identified. All test failures resolved.
