---
type: diagnostic-report
date: 2026-05-04
agent: debugger
status: root-cause-identified
---

# Diagnostic Report: 3 Failed ConversationFlowTests

## Executive Summary

**Root Cause**: TestHybridSearchService keyword matching fails due to Vietnamese diacritic mismatch between test queries and service patterns.

**Impact**: 3 integration tests failing - all related to product search/RAG retrieval
- StatePersistence_AcrossScopes_MaintainsSalesContext
- ProcessMessage_ReturningCustomer_UsesRememberedContactWithSoftConfirmation  
- ProcessMessage_AskFreeshipForMask_UsesCurrentPolicyInsteadOfTwoProductShortcut

**Fix Priority**: HIGH - blocks integration test suite

## Root Cause Analysis

### Evidence Chain

**TestHybridSearchService Implementation** (CustomWebApplicationFactory.cs:806-850):

```csharp
public Task<List<FusedResult>> SearchAsync(string query, ...)
{
    var queryLower = query.ToLowerInvariant();
    
    // Line 828-831: Kem chống nắng
    if (queryLower.Contains("kem chống nắng") || 
        queryLower.Contains("kcn") || 
        queryLower.Contains("chong nang"))
    
    // Line 833-836: Mặt nạ  
    if (queryLower.Contains("mặt nạ") || 
        queryLower.Contains("mn") || 
        queryLower.Contains("mat na"))
    
    // Line 838-841: Kem lụa
    if (queryLower.Contains("kem lụa") || 
        queryLower.Contains("kl") || 
        queryLower.Contains("lua"))
}
```

**Test Queries vs Patterns**:

| Test | Query | Pattern Check | Match? |
|------|-------|---------------|--------|
| Test 1 | "Toi muon mua kem lua" | Contains("kem lụa") ❌ / Contains("lua") ✓ | **SHOULD MATCH** |
| Test 2 | "Toi muon mua kem chong nang" | Contains("chong nang") ✓ | **SHOULD MATCH** |
| Test 3 | "mặt nạ ngủ" | Contains("mặt nạ") ✓ | **SHOULD MATCH** |

### The Real Problem

All 3 queries **should match** based on fallback patterns. The issue is NOT the keyword matching logic itself.

**Hypothesis**: The problem is likely in how TestHybridSearchService is being invoked or how the query is being preprocessed before reaching SearchAsync.

### Additional Evidence Needed

From user's initial report:
- Log shows: "Keyword search: m?t n? ng? 0 results"
- Log shows: "RAG retrieval failed"

The garbled characters "m?t n? ng?" suggest **encoding/normalization issue** before query reaches TestHybridSearchService.

## Competing Hypotheses

### H1: Unicode Normalization Issue (MOST LIKELY)
**Evidence**: Log shows "m?t n? ng?" instead of "mặt nạ ngủ"
**Mechanism**: Vietnamese diacritics being corrupted during string processing
**Test**: Check if query is being normalized/encoded incorrectly before SearchAsync

### H2: ToLowerInvariant() Breaks Vietnamese Diacritics
**Evidence**: .NET's ToLowerInvariant() may not preserve Vietnamese combining characters correctly
**Mechanism**: Culture-invariant lowercasing strips diacritics
**Test**: Compare ToLowerInvariant() vs ToLower(CultureInfo.InvariantCulture) behavior

### H3: Filter Parameter Mismatch
**Evidence**: TestHybridSearchService checks `filter["tenant_id"]` must equal primary tenant
**Mechanism**: If filter not passed correctly, returns empty results
**Test**: Verify tenant_id filter is being passed in test context

## Recommended Investigation Steps

1. **Add diagnostic logging to TestHybridSearchService.SearchAsync**:
   ```csharp
   Console.WriteLine($"[TestHybridSearchService] Query: '{query}'");
   Console.WriteLine($"[TestHybridSearchService] QueryLower: '{queryLower}'");
   Console.WriteLine($"[TestHybridSearchService] TenantId filter: '{tenantId}'");
   ```

2. **Run single test with verbose output** to capture logs:
   ```bash
   dotnet test --filter "FullyQualifiedName~StatePersistence_AcrossScopes_MaintainsSalesContext" --logger "console;verbosity=detailed"
   ```

3. **Check RAGService/ProductGroundingService** for query preprocessing:
   - Search for where HybridSearchService.SearchAsync is called
   - Check if query is being normalized/sanitized before passing to SearchAsync

4. **Verify tenant context** in test setup:
   - Confirm TenantId is set correctly in test scope
   - Verify filter dictionary is being constructed properly

## Immediate Fix Options

### Option A: Fix Unicode Handling (Recommended)
Replace `ToLowerInvariant()` with culture-aware lowercasing:
```csharp
var queryLower = query.ToLower(new CultureInfo("vi-VN"));
```

### Option B: Add Diacritic-Insensitive Matching
Normalize both query and patterns to remove diacritics:
```csharp
private string RemoveDiacritics(string text)
{
    var normalized = text.Normalize(NormalizationForm.FormD);
    var chars = normalized.Where(c => 
        CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
    return new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
}
```

### Option C: Expand Keyword Patterns (Quick Fix)
Add non-diacritic variants to all patterns:
```csharp
if (queryLower.Contains("kem lua") || queryLower.Contains("kem lụa") || 
    queryLower.Contains("kl") || queryLower.Contains("lua"))
```

## Next Steps

1. Add diagnostic logging to TestHybridSearchService
2. Run failed tests to capture actual query strings
3. Trace query flow from test → state machine → RAG → HybridSearchService
4. Implement fix based on confirmed root cause
5. Verify all 3 tests pass after fix

## Unresolved Questions

- Where is the query being corrupted to "m?t n? ng?"?
- Is this a console encoding issue or actual string corruption?
- Are there other places in codebase with similar Vietnamese text handling issues?
- Should we add Vietnamese text normalization as a standard preprocessing step?

## Files Requiring Investigation

- `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\tests\MessengerWebhook.IntegrationTests\CustomWebApplicationFactory.cs` (line 806-850)
- `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\Services\RAG\RAGService.cs`
- `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\Services\ProductGrounding\ProductGroundingService.cs`
- `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\StateMachine\ConversationStateMachine.cs`
