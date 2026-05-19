---
title: "Related products grounding fallback"
description: "Production-ready deterministic DB-grounded related suggestions when product need is vague or mistaken."
status: completed
priority: P1
effort: 10h
branch: master
tags: [sales-bot, product-grounding, hallucination-safety, multi-tenant]
created: 2026-04-26
---

# Related Products Grounding Fallback Plan

## Goal
When customer asks vague/mistaken product need (e.g. `tôi đang tìm sản phẩm mặt nạ dưỡng ẩm`), bot must not hallucinate. If DB has related active same-tenant products, return max 2-3 deterministic grounded suggestions. If none, keep existing safe fallback.

## Phases

| Phase | Status | Effort | Dependencies | File ownership |
|---|---:|---:|---|---|
| [01 - Contract + repository retrieval](phase-01-contract-and-repository-retrieval.md) | completed | 3h | none | `IProductRepository.cs`, `ProductRepository.cs` |
| [02 - Grounding service related suggestions](phase-02-grounding-service-related-suggestions.md) | completed | 3h | Phase 01 | `Services/ProductGrounding/*` |
| [03 - Handler integration + validation](phase-03-handler-integration-and-validation.md) | completed | 2h | Phase 02 | `SalesStateHandlerBase.cs` |
| [04 - Tests + docs sync](phase-04-tests-and-docs-sync.md) | completed | 2h | Phase 03 | `tests/MessengerWebhook.UnitTests/...`, `docs/*` if implementation changes behavior |

## Data Flow
Customer message → grounding need detection → active selected + RAG allowed products → if none, deterministic same-tenant DB related-product query → related suggestions formatted from DB facts only → response validation → customer. Selection context unchanged until customer chooses a product.

## Key Dependencies
- Existing Phase 1 grounding gate in `SalesStateHandlerBase.cs`.
- Tenant-safe product repository methods.
- `ResponseValidationService` must validate suggestion response against suggested grounded products.
- No DB migration unless existing schema cannot support category/search retrieval.

## Backward Compatibility
- Existing selected product flow unchanged.
- Existing safe fallback remains when no related products.
- No mutation of `selectedProductCodes` for suggestions.
- New repository method additive; unsafe existing methods not removed in this plan unless callers migrated separately.

## Success Criteria
- Vague product need with DB-confirmed related products returns max 2-3 grounded suggestions.
- No related products returns existing safe fallback.
- Inactive/other-tenant products excluded.
- No product hallucination or ungrounded price/policy claim.
- Suggestions do not mutate selected product context.
- `dotnet build` and relevant unit tests pass.

## Validation
- `dotnet test "tests/MessengerWebhook.UnitTests" --filter "ProductGrounding|ProductRepository|SalesStateHandlerBase" -p:UseAppHost=false`: passed 55/55.
- `dotnet test "tests/MessengerWebhook.UnitTests" -p:UseAppHost=false`: passed 620/620.
- `dotnet build -p:UseAppHost=false`: passed with 0 warnings and 0 errors.

## Risks
- High: unsafe tenant leak via broad category query. Mitigate by new tenant-scoped active query and tests.
- High: suggestions accidentally become selected products. Mitigate separate suggestion contract and tests.
- Medium: vague category mapping misses Vietnamese intent. Mitigate deterministic keyword/category mapping first; no Gemini invention.

## Rollback
Revert Phase 03 integration first to restore current fallback. Phase 01/02 additive code can remain unused or be reverted after tests pass.

## Implementation Command
After approval, cook from this plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260426-1909-related-products-grounding-fallback/plan.md`
