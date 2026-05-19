---
title: "Phase 02 - Grounding service related suggestions"
description: "Build deterministic DB-grounded suggestions without letting Gemini invent alternatives."
status: completed
priority: P1
effort: 3h
branch: master
tags: [product-grounding, suggestions, hallucination-safety]
created: 2026-04-26
---

# Phase 02 - Grounding Service Related Suggestions

## Context Links
- Overview: `plan.md`
- Depends on: `phase-01-contract-and-repository-retrieval.md`
- Files: `src/MessengerWebhook/Services/ProductGrounding/*`

## Overview
Extend product grounding with deterministic related-product suggestions. This is not prompt-only: related products come only from tenant-safe DB retrieval. Suggestions are not selected products.

## Requirements
### Functional
- Detect vague/mistaken product needs that require grounding but have no currently allowed product.
- Infer relation criteria deterministically from message: category terms such as `mặt nạ` are primary filters; need/benefit terms such as `dưỡng ẩm`, `cấp ẩm`, `phục hồi`, `da khô` only refine ranking/filtering inside that category. If category is present but no same-category product exists, do not broaden to unrelated categories in this phase.
- Retrieve DB-confirmed active same-tenant related products.
- Return max 2-3 suggestions with product name/code/price only when DB value exists.
- If no related products, return existing `ProductGroundingService.FallbackReply` unchanged.
- Suggestions must not mutate `selectedProductCodes`.

### Non-functional
- KISS: no LLM classifier for suggestions in this phase.
- DRY: reuse existing `GroundedProduct` model where possible; add explicit suggestion wrapper only if selection semantics need clarity.
- Deterministic output; no random ranking.

## Proposed Data Contract
Add/extend product grounding models:
- `GroundedProductContext.RelatedSuggestions: IReadOnlyList<GroundedProduct>` or a new `GroundedProductSuggestionContext` with explicit fields.
- `HasRelatedSuggestions => RelatedSuggestions.Count > 0`.
- `RelatedSuggestionReply` builder returns final customer message from grounded products.

Important semantic distinction:
- `AllowedProducts`: products Gemini may mention for the current response validation.
- `RelatedSuggestions`: products displayed as options, but not persisted as selected.
- Do not write suggestions into `ctx.SetData("selectedProductCodes", ...)`.

## Architecture / Data Flow
Input: customer message + tenant id + current active selected products + RAG products.
Transform:
1. Existing `BuildContext` still detects `RequiresGrounding` and allowed products.
2. If `RequiresGrounding && !HasAllowedProducts`, infer related criteria from message.
3. Query repository for active same-tenant related products, max 3.
4. Convert Products → `GroundedProduct` suggestions.
5. Build deterministic Vietnamese reply:
   - Acknowledge not finding exact requested item if applicable.
   - Offer `2-3` DB-confirmed related products.
   - Ask customer to choose one for details/order.
   - Include price only when `BasePrice` exists; no policy/inventory claims unless validated facts available.
Exit: `GroundedProductContext` with `RelatedSuggestions` and a safe suggestion reply, or original fallback.

Example response shape:
`Dạ hiện em chưa thấy đúng sản phẩm "mặt nạ dưỡng ẩm" trong catalog. Em có vài sản phẩm liên quan đang có dữ liệu trên hệ thống: 1) {Name} ({Code}) - {Price}. 2) ... Chị muốn xem sản phẩm nào ạ?`

## Related Code Files
### Modify
- `src/MessengerWebhook/Services/ProductGrounding/GroundedProductContext.cs`
- `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs`
- `src/MessengerWebhook/Services/ProductGrounding/ProductNeedDetector.cs` if criteria extraction belongs there.
- Existing detectors under `src/MessengerWebhook/Services/ProductGrounding/*` only as needed.

### Create
- Prefer none. If file size/clarity demands, create focused PascalCase C# file in same folder, e.g. `ProductRelatedSuggestionService.cs`.

### Delete
- None.

## Implementation Steps
1. Define suggestion fields on context; keep constructor backwards-compatible if tests need small updates.
2. Add deterministic criteria extraction function. Start with simple normalized Vietnamese keyword/category mapping, no Gemini.
3. Add service method to build context with related suggestions. If repository dependency requires DI, move `ProductGroundingService` from manual `new` in handler to DI in Phase 03.
4. Add reply builder that formats only DB facts.
5. Ensure suggested products are included in validation allowed products for that response only.
6. Keep fallback path identical when suggestion list empty.

## Todo List
- [x] Add explicit suggestion data contract.
- [x] Add deterministic criteria extractor.
- [x] Add DB-backed suggestion retrieval call boundary.
- [x] Add safe response builder.
- [x] Ensure validation can use suggestions as allowed mentions.

## Validation Results
- `GroundedProductContext` now separates `AllowedProducts` from `RelatedSuggestions` and exposes a deterministic `RelatedSuggestionReply`.
- `ProductGroundingService` extracts related criteria deterministically, retrieves DB-backed suggestions through the tenant-safe repository contract, and preserves the existing fallback when no suggestions exist.
- Suggestion replies include only grounded name/code/price facts and are eligible for validation using suggested products as allowed mentions.
- `dotnet test "tests/MessengerWebhook.UnitTests" --filter "ProductGrounding|ProductRepository|SalesStateHandlerBase" -p:UseAppHost=false`: passed 55/55.
- `dotnet test "tests/MessengerWebhook.UnitTests" -p:UseAppHost=false`: passed 620/620.
- `dotnet build -p:UseAppHost=false`: passed with 0 warnings and 0 errors.

## Success Criteria
- Vague need with related DB products produces max 3 grounded suggestions.
- No criteria or no DB products returns exact existing fallback.
- Suggested response never contains product not present in `RelatedSuggestions`.
- Suggested response never claims stock availability, promotion, policy, or effects beyond DB facts.

## Risk Assessment
| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| Gemini invents alternatives | Medium | High | Do not call Gemini for fallback suggestion; deterministic builder only |
| Criteria extractor too broad | Medium | Medium | Empty/uncertain criteria returns no suggestions; fallback safe |
| Suggestion treated as selected | Medium | High | Separate `RelatedSuggestions`; handler tests assert context unchanged |
| Price claim ungrounded | Low | High | Price only from `BasePrice`; validation allowed facts limited |

## Security Considerations
- Never bypass tenant-safe repository.
- Suggestions are product facts; no personal/customer data involved.
- Avoid logging raw customer message beyond existing logging policy.

## Test Matrix
- Unit: criteria extractor maps `mặt nạ dưỡng ẩm` to expected category/terms.
- Unit: empty/unknown vague request returns no criteria.
- Unit: response builder includes only provided suggestion names/codes/prices.
- Unit: max suggestions = 3.
- Unit: no suggestions preserves `FallbackReply`.

## Rollback Plan
Disable Phase 03 call path to stop using suggestions. Service changes are additive. If contract breaks tests, revert context extension and builder together.

## Next Steps
Phase 03 integrates suggestion context into both treatment and control/direct fallback branches.
