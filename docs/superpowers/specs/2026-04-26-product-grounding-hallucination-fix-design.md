# Product Grounding Hallucination Fix Design

## Context

Messenger sales bot can mention a product name that does not exist in runtime DB or Pinecone. Example: customer asks for `mặt nạ dưỡng ẩm`, bot replies with `Mặt nạ Tảo Biển Tươi Múi Xù`.

Observed root causes:

- Product grounding is partly inferred from customer message instead of enforced from response content.
- `RequiresProductGrounding(string message)` is too narrow for category/need queries like `mặt nạ dưỡng ẩm`.
- Gemini natural reply can emit product-like names when no active product is selected.
- Validation allowed products are built from active selected products, not from RAG/grounding candidates.
- Assistant history can preserve a hallucinated product name and feed it back to Gemini later.
- Product benefit copy has hard-coded branches by product code, which can drift from runtime product facts.

## Goals

- Prevent product names outside verified catalog/runtime data from reaching customers.
- Treat product retrieval as candidate discovery, not source of truth.
- Make grounding deterministic and testable outside prompt wording.
- Keep phase 1 small enough to ship without DB migration.
- Add structured product facts in phase 2 for durable suitability and benefit-claim validation.

## Non-goals

- Replace Gemini or vector search.
- Redesign full state machine.
- Build a new admin UI in phase 1.
- Guarantee perfect recommendation ranking in phase 1.

## Recommended approach

Implement in phases.

### Phase 1: Product Grounding Gate without schema migration

Add a domain-level grounding gate before and after Gemini.

#### Components

1. `ProductGroundingService`
   - Input: state context, customer message, active selected products, RAG candidates.
   - Output: `GroundedProductContext`.
   - Decides whether the message/response requires product grounding.
   - Produces allowed products and fallback reply.

2. `ProductNeedDetector`
   - Rule-based detector for category/need language.
   - Examples:
     - `mặt nạ`, `mat na` -> product category/reference.
     - `dưỡng ẩm`, `cấp ẩm`, `khô`, `thiếu ẩm` -> hydration need.
     - `nám`, `tàn nhang`, `đốm nâu` -> pigmentation need.
   - Deterministic and unit-testable.

3. `ProductMentionDetector`
   - Detects product-like mentions in bot responses.
   - Reuses or extends current product regex patterns from response validation.

4. `ResponseValidationService` grounding extension
   - Response is invalid if it mentions product-like name not in allowed products.
   - Grounding is required if customer message has product category/need OR response mentions a product-like name.

5. History sanitization
   - Before sending history to Gemini, remove or neutralize assistant turns that mention unverified product-like names.
   - Only validated assistant replies should be saved normally.

#### Flow

1. Handler receives customer message.
2. Build active product list from selected product codes.
3. Retrieve RAG candidates if enabled.
4. Build `GroundedProductContext` from active products + RAG products.
5. If message requires product grounding and no allowed products exist, return safe fallback before Gemini.
6. Send Gemini prompt with formatted allowed product context.
7. Validate generated response against allowed product names/codes.
8. If invalid, return fallback and do not persist hallucinated response as normal assistant history.
9. If valid, save response and continue.

#### Safe fallback

Use one consistent fallback:

`Dạ hiện em chưa tìm thấy dữ liệu sản phẩm phù hợp trong catalog để báo chính xác ạ. Chị cho em tên hoặc mã sản phẩm cụ thể, hoặc để em chuyển bạn hỗ trợ kiểm tra lại giúp mình nha.`

#### Acceptance criteria

- `mặt nạ dưỡng ẩm` requires product grounding.
- If RAG/DB returns no allowed product, bot returns fallback and does not ask Gemini to invent one.
- If Gemini returns `Mặt nạ Tảo Biển Tươi Múi Xù` and that name is not allowed, validation blocks it.
- Product names from active selected product or RAG DB-verified products are allowed.
- Assistant history sent to Gemini does not contain unverified hallucinated product names.

### Phase 2: Structured Product Facts

Add durable product facts so suitability and benefit claims do not depend on free-form description or hard-coded product code branches.

#### Data model options

Preferred: JSONB-like fact payload or normalized tags, depending on current EF/migration cost.

Minimum fact fields:

- `ProductType`: mask, cream, serum, toner, cleanser, sunscreen, combo, other.
- `Benefits`: hydration, repair, brightening, pigmentation-care, oil-control, acne-care, sun-protection, cleansing.
- `SkinConcerns`: dry-skin, oily-skin, acne, pigmentation, dullness, sensitive-skin.

#### Matching rules

- Retrieval returns candidates.
- Fact matcher filters candidates by product type and benefits.
- Example: `mặt nạ dưỡng ẩm` requires product type `mask` and benefit `hydration` when such structured facts exist.
- If no candidate satisfies facts, fallback or clarify instead of recommending weakly matched products.

#### Benefit claim validation

Response claim terms map to benefit tags:

- `dưỡng ẩm`, `cấp ẩm`, `thiếu ẩm` -> hydration.
- `phục hồi` -> repair.
- `trắng`, `sáng da` -> brightening.
- `nám`, `tàn nhang`, `đốm nâu` -> pigmentation-care.
- `chống nắng`, `UV`, `SPF` -> sun-protection.

If response claims a benefit for a mentioned product, that product must have the matching fact tag.

#### Remove drift-prone code

Remove hard-coded benefit copy keyed only by product code, especially code branches such as `Code == "MN"` adding hydration/repair text. Use structured facts and product description instead.

### Phase 3: Observability and data hygiene

Add diagnostics so future incidents can be traced quickly.

- Log customer query, active product codes, RAG product IDs/names, allowed products, blocked product mentions, and fallback reason.
- Add a read-only debug endpoint or admin operation for `query -> candidates -> grounded decision`.
- Document rebuild/upsert process for Pinecone after catalog/facts changes.

## Prompt changes

Prompt remains a secondary safety layer, not the source of truth.

Add explicit allowed-product instruction:

```text
ALLOWED PRODUCT NAMES:
{ALLOWED_PRODUCT_NAMES}

Nếu cần nêu tên sản phẩm, chỉ được dùng chính xác tên trong ALLOWED PRODUCT NAMES.
Nếu danh sách rỗng, không được gợi ý tên sản phẩm. Hãy nói chưa tìm thấy sản phẩm phù hợp.
```

## Testing plan

### Unit tests

- `ProductNeedDetector` detects product grounding for `mặt nạ dưỡng ẩm`.
- Grounding context returns fallback when no allowed product exists.
- Response validation blocks unknown product-like names.
- Response validation allows names in allowed products.
- History sanitizer removes assistant turns containing unverified product names.

### Regression tests

- Customer: `mặt nạ dưỡng ẩm`.
- Runtime catalog does not contain `Mặt nạ Tảo Biển Tươi Múi Xù`.
- Simulated Gemini response contains that name.
- Expected: final bot response is fallback, not hallucinated name.

### Build validation

- Run `dotnet build`.
- Run relevant unit tests.
- Run full `dotnet test` before final handoff if feasible.

## Risks

- Phase 1 can be conservative and fallback more often for generic category/need queries.
- Regex product mention detection may over-capture long Vietnamese phrases.
- RAG candidates may be relevant but not selected if facts are missing in phase 2.

## Mitigations

- Keep fallback wording helpful and ask for product name/code or handoff.
- Add tests around regex trimming.
- Roll out phase 2 after catalog facts are populated.
- Log blocked responses to tune rules without exposing hallucinations to customers.

## Rollout

1. Implement phase 1 and tests.
2. Verify the exact incident no longer leaks unknown product names.
3. Update docs/changelog.
4. Plan phase 2 data model and migration separately.
5. Add observability after grounding behavior is stable.

## Open questions

- Which tenant/catalog is the source of truth for phase 2 fact tagging?
- Should phase 2 facts be stored as JSON payload or normalized tag table?
- Should fallback always offer human handoff, or only after repeated unmatched product queries?
