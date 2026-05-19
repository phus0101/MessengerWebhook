# Phase 1 - Product Grounding Gate

## Context Links

- Design spec: `docs/superpowers/specs/2026-04-26-product-grounding-hallucination-fix-design.md`
- Sales flow: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- RAG retrieval: `src/MessengerWebhook/Services/RAG/RAGService.cs`
- RAG formatting: `src/MessengerWebhook/Services/RAG/ContextAssembler.cs`
- Validation: `src/MessengerWebhook/Services/ResponseValidation/ResponseValidationService.cs`
- Prompt: `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`

## Overview

Priority: Critical correctness fix.

Current status: Completed and validated on 2026-04-26.

Implemented a deterministic product grounding gate that blocks product names not verified by active product context or RAG DB-confirmed products. This phase avoids DB schema changes.

## Key Insights

- `RequiresProductGrounding(string message)` is too narrow for category/need queries.
- Response validation currently uses active selected products, not RAG product names.
- Gemini can emit product-like names in natural reply path.
- Assistant history can carry hallucinated product names into later prompts.

## Requirements

### Functional

- Detect product category/need messages such as `mặt nạ dưỡng ẩm` as requiring grounding.
- Build allowed products from active selected products plus DB-confirmed RAG products.
- Return safe fallback when product grounding is required but no allowed product exists.
- Validate generated responses against allowed product names/codes.
- Block unknown product-like names, including `Mặt nạ Tảo Biển Tươi Múi Xù` when absent from allowed products.
- Sanitize assistant history before sending it to Gemini.

### Non-functional

- No DB migration in phase 1.
- Keep implementation small and testable.
- Preserve tenant isolation.
- Avoid prompt-only fixes.

## Architecture

### New/updated units

1. `ProductNeedDetector`
   - Rule-based category/need detection.
   - No external dependencies.

2. `ProductMentionDetector`
   - Extracts product-like names from response/history text.
   - Shared by grounding and validation where practical.

3. `ProductGroundingService`
   - Builds `GroundedProductContext`.
   - Depends on current active products and RAG context products.

4. `RAGContext` update
   - Add structured product summary list so handlers can pass RAG product names/codes to validation.

5. `ResponseValidationService` update
   - Requires grounding when response contains product-like mention.
   - Blocks product-like names outside allowed list.

6. `GeminiService` or caller-side history sanitizer
   - Ensures unverified assistant product mentions are not resent to Gemini.

## Related Code Files

### Modify

- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Services/RAG/RAGService.cs`
- `src/MessengerWebhook/Services/RAG/ContextAssembler.cs`
- `src/MessengerWebhook/Services/RAG/IRAGService.cs`
- `src/MessengerWebhook/Services/ResponseValidation/ResponseValidationService.cs`
- `src/MessengerWebhook/Services/ResponseValidation/Models/ResponseValidationContext.cs`
- `src/MessengerWebhook/Services/AI/GeminiService.cs` if sanitizer is best placed there
- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- relevant DI registration in `src/MessengerWebhook/Program.cs`
- unit tests under `tests/`

### Create if needed

- `src/MessengerWebhook/Services/ProductGrounding/ProductNeedDetector.cs`
- `src/MessengerWebhook/Services/ProductGrounding/ProductMentionDetector.cs`
- `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs`
- `src/MessengerWebhook/Services/ProductGrounding/GroundedProductContext.cs`
- interfaces only if needed by tests/DI.

## Implementation Steps

1. Add product grounding models and detectors.
   - Implement deterministic category/need phrase detection.
   - Implement product-like mention extraction with conservative trimming.

2. Extend RAG context with structured product facts available today.
   - Include product id, code, name, category, price if already available.
   - Keep existing formatted context behavior.

3. Implement `ProductGroundingService`.
   - Merge active products + RAG products into allowed products.
   - Deduplicate by product id/code/name.
   - Provide fallback reply.

4. Wire grounding into `SalesStateHandlerBase` natural reply flow.
   - Build grounding before Gemini.
   - Return fallback when required and no allowed products exist.
   - Pass allowed products to validation.

5. Update response validation.
   - Ground if customer message needs product grounding OR response has product-like mention OR allowed products exist.
   - Unknown product-like name is a grounding error.

6. Sanitize history.
   - Remove or neutralize assistant history turns with product-like names outside allowed catalog/context.
   - Keep user messages unchanged.

7. Update prompt.
   - Add allowed product names instruction.
   - Keep prompt as secondary safety only.

8. Add tests.
   - Detector tests.
   - Validation tests.
   - Sales flow regression using fake Gemini response if existing test seams allow it.

9. Validate.
   - Run targeted tests.
   - Run `dotnet build`.
   - Run broader `dotnet test` if time/runtime permits.

## Todo List

- [x] Add detectors and grounding context.
- [x] Extend RAG context structured products.
- [x] Add grounding service.
- [x] Wire handler flow.
- [x] Strengthen response validation.
- [x] Sanitize assistant history.
- [x] Update prompt.
- [x] Add regression tests.
- [x] Build and test.
- [x] Update docs/changelog if implementation changes warrant it.

## Success Criteria

- `mặt nạ dưỡng ẩm` is treated as grounded product need. Done.
- Unknown product names are blocked even when Gemini generates them. Done.
- Valid RAG/active product names are not blocked. Done.
- No DB migration required. Done.
- Tests prove the exact hallucinated name cannot leak. Done.
- Build succeeds. Done.

## Validation Results

- `dotnet build`: passed with 0 warnings and 0 errors.
- Targeted unit tests: passed 77/77.
- Full unit tests: passed 610/610.
- Independent tester: passed build, targeted tests, and full unit suite.
- Code review: no blocker for the Phase 1 incident class.

## Risk Assessment

- Risk: regex over-captures Vietnamese phrases.
  - Mitigation: trim punctuation/CTA fragments and test examples.

- Risk: more fallbacks for vague product queries.
  - Mitigation: keep fallback helpful and use RAG products when DB-confirmed.

- Risk: handler file grows larger.
  - Mitigation: keep new logic in ProductGrounding service files.

## Security Considerations

- Maintain tenant isolation by only accepting products already loaded through tenant-aware active product/RAG paths.
- Do not expose internal product IDs in customer-facing fallback.
- Avoid logging full sensitive customer profile; log only product grounding metadata.

## Next Steps

Phase 1 is complete. Phase 2 can add structured product facts and benefit-claim validation with DB migration.

## Follow-up Considerations

- Short lowercase or code-only product mentions may need extra Phase 2 hardening if they are valid customer-facing identifiers.
- `ProductGroundingService` and detectors can be moved to DI in a cleanup pass.

## Unresolved Questions

- None for Phase 1 closure.
