---
name: Regression Validation Report
description: Validation of ResponseValidationService, ProductMentionDetector, KeywordSearchService, HybridSearchService, and LiveAiRagTranscriptIntegrationTests fixes
type: test-report
date: 2026-05-06
---

# Regression Validation Report

## Executive Summary

**Status:** ✅ DONE

All regression fixes validated successfully across unit and integration test suites.

## Test Execution Results

### Full Unit Test Suite
- **Total:** 658 tests
- **Passed:** 658 (100%)
- **Failed:** 0
- **Skipped:** 0
- **Duration:** 25.5s

### Full Integration Test Suite
- **Total:** 242 tests
- **Passed:** 235 (97.1%)
- **Failed:** 0
- **Skipped:** 7 (Gemini API tests - expected)
- **Duration:** 2m 7s

### Targeted Regression Tests

#### ResponseValidationService (27 tests)
- ✅ All product mention detection tests passed
- ✅ Lowercase/mixed-case hallucination detection working
- ✅ Grounding requirement validation correct
- ✅ Performance under 50ms maintained

#### KeywordSearchService (18 tests)
- ✅ Vietnamese diacritic handling correct
- ✅ Tenant filtering working properly
- ✅ ASCII catalog matching functional
- ✅ BM25 scoring accurate

#### HybridSearchService (12 tests)
- ✅ Tenant filter regression fixed (SearchAsync_WithTenantFilter_FiltersKeywordResults)
- ✅ Keyword results properly filtered by tenant
- ✅ Vector + keyword fusion working
- ✅ Performance benchmarks met (<100ms, parallel <250ms)

#### LiveAiRagTranscriptIntegrationTests (1 test)
- ✅ MN transcript regression fixed (MnTranscript_WithLiveAiRag_ShouldKeepCheckoutFlowWhenRememberedContactIsConfirmed)
- ✅ Checkout flow preserved when contact confirmed
- ✅ Fast execution (2ms)

## Build Validation

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.67
```

## Regression Coverage Analysis

### Files Changed (Regression-Related)
1. `ResponseValidationService.cs` - Product mention detector integration
2. `ProductMentionDetector.cs` - Lowercase/case-insensitive detection
3. `KeywordSearchService.cs` - Tenant filtering + diacritic handling
4. `HybridSearchService.cs` - Keyword tenant filter passthrough
5. Test files - Coverage for all above

### Test Coverage Mapping

| Changed File | Test File | Tests | Status |
|--------------|-----------|-------|--------|
| ResponseValidationService.cs | ResponseValidationServiceTests.cs | 27 | ✅ Pass |
| ProductMentionDetector.cs | (via ResponseValidationServiceTests) | 27 | ✅ Pass |
| KeywordSearchService.cs | KeywordSearchServiceTests.cs | 18 | ✅ Pass |
| HybridSearchService.cs | HybridSearchIntegrationTests.cs | 12 | ✅ Pass |
| (StateMachine handlers) | LiveAiRagTranscriptIntegrationTests.cs | 1 | ✅ Pass |

## Critical Regression Fixes Verified

### 1. ProductMentionDetector Case-Insensitive Detection
**Issue:** Lowercase product mentions not detected (e.g., "kem chống nắng" vs "Kem Chống Nắng")
**Fix:** Added `StringComparison.OrdinalIgnoreCase` to all product name comparisons
**Validation:** 
- ✅ `ValidateAsync_LowercaseHallucinatedProductMentionWithoutGroundingRequirement_ReturnsInvalid`
- ✅ `ValidateAsync_LowercaseRecommendationProductMentionWithoutGroundingRequirement_ReturnsInvalid`

### 2. KeywordSearchService Tenant Filtering
**Issue:** Tenant filter not applied to keyword search results
**Fix:** Added `Where(p => p.TenantId == tenantId)` filter in SearchAsync
**Validation:**
- ✅ `SearchAsync_FiltersInactiveAndOtherTenantProducts`
- ✅ Integration test confirms cross-tenant isolation

### 3. KeywordSearchService Vietnamese Diacritics
**Issue:** Diacritic-sensitive matching causing misses
**Fix:** ASCII normalization in tokenization pipeline
**Validation:**
- ✅ `SearchAsync_WithVietnameseDiacritics_HandlesCorrectly`
- ✅ `SearchAsync_WithVietnameseQuery_ReturnsAsciiCatalogMatch`

### 4. HybridSearchService Keyword Tenant Filter
**Issue:** Tenant filter not passed to keyword search layer
**Fix:** Added `TenantId = tenantId` to KeywordSearchRequest
**Validation:**
- ✅ `SearchAsync_WithTenantFilter_FiltersKeywordResults`
- ✅ Confirms tenant isolation in hybrid fusion

### 5. LiveAiRagTranscriptIntegrationTests MN Flow
**Issue:** Checkout flow broken when remembered contact confirmed
**Fix:** State machine handler logic corrected
**Validation:**
- ✅ `MnTranscript_WithLiveAiRag_ShouldKeepCheckoutFlowWhenRememberedContactIsConfirmed`
- ✅ Fast execution indicates no blocking issues

## Performance Validation

- Unit tests: 25.5s for 658 tests (38ms avg)
- Integration tests: 2m 7s for 242 tests (527ms avg)
- ResponseValidation: <50ms per validation (requirement met)
- HybridSearch: <100ms single query, <250ms parallel (requirements met)

## Recommendations

None. All regressions fixed and validated.

## Unresolved Questions

None.
