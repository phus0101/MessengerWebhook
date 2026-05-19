# Tenant Isolation Audit — 2026-05-13

**Plan:** `plans/260508-1039-production-stabilization/phase-04-tenant-isolation-audit.md`
**Scope:** Multi-tenant data isolation audit across DB, cache, vector search, and API endpoints

---

## Findings

### CRITICAL: 1

- `Endpoints/InternalOperationsEndpointExtensions.cs` — GET `/internal/draft-orders`, GET `/internal/support-cases`, POST `/support-cases/{id}/complete`, POST `/knowledge/import` had NO authentication. Anyone with network access could call these endpoints.

### HIGH: 1

- `Services/Admin/AdminAuthService.cs` (17 callsites) + `Services/FacebookPageConfigLookupService.cs` (3 callsites) + `Services/MessengerService.cs` (1 callsite) + `Services/WebhookProcessor.cs` (2 callsites) — total 23 `IgnoreQueryFilters()` without `// ALLOW: <reason>` justification.

### MEDIUM: 1

- `Services/Cache/CacheKeyGenerator.cs` — `GenerateResponseKey` parameter named `context` but callers pass `tenantId` there. Misleading API; functionally correct but fragile.

---

## Verified Clean

| Area | Status | Notes |
|------|--------|-------|
| EF Global Query Filters | ✅ Clean | 19/19 multi-tenant entities covered |
| Raw SQL (pgvector) | ✅ Clean | Both queries have `@tenantId` param |
| `GenerateResultKey` cache | ✅ Clean | Includes tenantId in key |
| Admin endpoints | ✅ Clean | `RequireAuthorization()` + AdminUserContext |
| Vector search isolation | ✅ Clean | pgvector via EF filters (Pinecone not used) |
| Background services | ✅ Acceptable | LiveCommentAutomationService relies on EF filters; no standalone leak path |

---

## Fixes Applied

| # | File | Change |
|---|------|--------|
| 1 | `InternalOperationsEndpointExtensions.cs` | Added `X-Internal-Api-Key` validation to 4 unprotected endpoints |
| 2 | `AdminAuthService.cs` | Added `// ALLOW: <reason>` to 17 callsites |
| 3 | `FacebookPageConfigLookupService.cs` | Added `// ALLOW: <reason>` to 3 callsites |
| 4 | `MessengerService.cs` | Added `// ALLOW: <reason>` to 1 callsite |
| 5 | `WebhookProcessor.cs` | Added `// ALLOW: <reason>` to 2 callsites |
| 6 | `CacheKeyGenerator.cs` | Renamed `context` → `tenantId` in `GenerateResponseKey` |
| 7 | `TenantIsolationGuardrailTests.cs` | Created CI guardrail — fails build if `IgnoreQueryFilters()` lacks `// ALLOW:` |
| 8 | `CacheKeyGeneratorTests.cs` | Added `GenerateResponseKey_DifferentTenantId_ReturnsDifferentKeys` |

---

## Test Coverage

- `TenantIsolationTests.cs` — 6 integration tests covering Product, CustomerIdentity, DraftOrder, ConversationSession, VipProfile, RiskSignal isolation (pre-existing, all passing)
- `TenantIsolationGuardrailTests.cs` — CI guardrail (new, passing: 1/1)
- `CacheKeyGeneratorTests.cs` — 14 tests including new tenant isolation assertion

---

## Unresolved Questions

1. `MetricsControllerTests.GetSummary_ReturnsCorrectData` integration test was failing before this phase — pre-existing issue with JWT claim propagation in test setup, not related to tenant isolation.
2. `/internal/support-cases/{id}/complete` (GET) is secured by time-limited token — does NOT require `X-Internal-Api-Key`. Token mechanism reviewed and accepted as secure for email link flow.
3. `GenerateEmbeddingKey` intentionally cross-tenant (same text → same embedding regardless of tenant) — per plan: "cố ý không tenant-scoped, OK".
