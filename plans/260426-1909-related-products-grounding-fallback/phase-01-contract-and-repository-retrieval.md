---
title: "Phase 01 - Contract and repository retrieval"
description: "Add tenant-safe active related-product retrieval without schema migration."
status: completed
priority: P1
effort: 3h
branch: master
tags: [repository, tenant-isolation, product-grounding]
created: 2026-04-26
---

# Phase 01 - Contract and Repository Retrieval

## Context Links
- Overview: `plan.md`
- Related existing plan: `../260426-1139-product-grounding-hallucination-fix/plan.md`
- Files: `src/MessengerWebhook/Data/Repositories/IProductRepository.cs`, `src/MessengerWebhook/Data/Repositories/ProductRepository.cs`

## Overview
Add an additive repository contract for active same-tenant related products. Do not use existing `GetByCategoryAsync(ProductCategory category)` because it lacks tenant filter. Avoid DB migration unless existing columns cannot support production behavior.

## Requirements
### Functional
- Retrieve active products scoped by `TenantId`.
- Support deterministic relation from customer need: category and/or normalized search terms.
- Return stable order and max count controlled by caller.
- Include enough facts for grounding: Id, Code, Name, Category, BasePrice, optionally Variants/Images only if existing response needs them.

### Non-functional
- No cross-tenant data leak.
- No inactive products.
- Bounded query result (`Take(maxCount)` after deterministic ordering/filtering).
- Async EF query; no in-memory scan across all products.

## Proposed Data Contract
Add to `IProductRepository.cs`:
- `Task<List<Product>> GetActiveRelatedAsync(Guid tenantId, ProductCategory? category, IReadOnlyCollection<string> normalizedTerms, int maxCount, CancellationToken cancellationToken = default);`

Contract rules:
- `tenantId` required.
- `maxCount` clamp to 1..3 or 1..5 inside repository/service; production response uses 2-3.
- `category == null` and empty terms returns empty list, not broad catalog.
- Terms are already normalized by service; repository only applies safe `Name/Code` containment if provider supports it.

## Architecture / Data Flow
Input: `tenantId`, optional inferred `ProductCategory`, normalized terms, max count.
Transform:
1. Start EF query `Products.Where(p => p.TenantId == tenantId && p.IsActive)`.
2. Add category filter when available.
3. Add term filter only against product `Name`/`Code` and only after normalization strategy chosen.
4. Order deterministic: exact category match first (if mixed), then `Name`, then `Code`/`Id`.
5. `Take(maxCount)`.
Exit: DB-confirmed `List<Product>`.

## Related Code Files
### Modify
- `src/MessengerWebhook/Data/Repositories/IProductRepository.cs`
- `src/MessengerWebhook/Data/Repositories/ProductRepository.cs`

### Create
- None unless implementation needs a small existing-pattern helper in same repository area.

### Delete
- None.

## Implementation Steps
1. Add tenant-safe repository interface method.
2. Implement EF query in `ProductRepository` with `AsNoTracking()` if current style allows; preserve Includes only if needed by response facts.
3. Clamp `maxCount`; empty criteria returns empty result.
4. Do not modify existing unsafe method unless all callers are audited in a separate task.
5. Add logging only at caller/service layer; repository stays simple.

## Todo List
- [x] Add interface method.
- [x] Implement tenant filter + active filter.
- [x] Implement bounded deterministic ordering.
- [x] Ensure empty criteria cannot return broad catalog.
- [x] Confirm no migration needed.

## Validation Results
- Repository contract implemented in `IProductRepository`/`ProductRepository` with active same-tenant filtering, empty-criteria guard, deterministic ordering, and bounded max count.
- `dotnet test "tests/MessengerWebhook.UnitTests" --filter "ProductGrounding|ProductRepository|SalesStateHandlerBase" -p:UseAppHost=false`: passed 55/55.
- `dotnet test "tests/MessengerWebhook.UnitTests" -p:UseAppHost=false`: passed 620/620.
- `dotnet build -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- No DB migration required.

## Success Criteria
- Query cannot return products from another tenant.
- Query cannot return inactive products.
- Query returns <= requested max.
- Query stable order for same DB state.
- Existing callers still compile after additive method.

## Risk Assessment
| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| Tenant leak from using old method | Medium | High | New method requires `tenantId`; tests assert exclusion |
| Broad catalog returned for vague need | Medium | High | Empty criteria returns empty; max count clamp |
| EF translation issue from normalization | Medium | Medium | Normalize before query; use simple `ToLower`/provider-safe pattern only if supported |
| Over-fetch with Includes | Low | Medium | Include only fields needed; no variants unless response uses them |

## Security Considerations
- Tenant isolation mandatory.
- Do not expose unavailable/inactive inventory.
- No confidential fields in response model.

## Test Matrix
- Unit or repository test: tenant A request excludes tenant B product.
- Unit/repository test: inactive product excluded.
- Unit/repository test: no criteria returns empty.
- Unit/repository test: max count honored.

## Rollback Plan
Because method is additive, rollback by removing Phase 03 caller first. Then remove interface/implementation if unused. No data migration rollback.

## Next Steps
Phase 02 consumes this method through product-grounding suggestion service logic.
