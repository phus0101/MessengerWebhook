# Phase 04: Tenant Isolation Audit — Mostly Clean, One Critical Gap Found

**Date**: 2026-05-13
**Severity**: High (auth bypass on 4 internal endpoints)
**Component**: Multi-tenant isolation, internal APIs
**Status**: Resolved

## What Happened

Ran comprehensive audit on tenant isolation across 19 multi-tenant entities. Expected to find entity-level leaks. Found almost none. Found instead: four `/internal/*` endpoints with **zero authentication**, authenticable via `X-Internal-Api-Key` only in one existing endpoint. Fixed both.

## The Brutal Truth

The good news was a relief—EF Global Query Filters are doing their job. The bad news stung more because it shouldn't have existed: unauthenticated internal endpoints sitting in production. `/internal/draft-orders`, `/internal/support-cases`, `/support-cases/{id}/complete`, and `/knowledge/import` had no auth checks. Same file had `/internal/alerts/seq` *already* protected with the right pattern. We just didn't copy it everywhere.

## Technical Details

**CRITICAL Fix**: `InternalOperationsEndpointExtensions.cs`
- Added `X-Internal-Api-Key` validation to 4 endpoints
- Pattern matched existing `/internal/alerts/seq` implementation
- No token exposure in requests—key checked against `INTERNAL_API_KEY` env var

**HIGH Fix**: 23 `IgnoreQueryFilters()` callsites
- AdminAuthService, FacebookPageConfigLookupService, MessengerService, WebhookProcessor
- All legitimate (admin bootstrap, design-time cross-tenant lookups)
- Added `// ALLOW: <reason>` comments to every one; CI guardrail enforces future compliance

**Minor**: `CacheKeyGenerator.GenerateResponseKey` parameter renamed `context` → `tenantId`. API was misleading; behavior unchanged.

## Why This Mattered

19 entities, 6 integration tests, strict filter discipline—but four endpoints just... didn't follow the pattern. Copy-paste inconsistency, not architectural failure.

## Next Steps

- CI guardrail test live: `TenantIsolationGuardrailTests.cs` blocks builds without `// ALLOW:` comments
- All internal endpoints now consistent on auth strategy
- Monitor endpoint usage logs for historical unauthorized access attempts

**Commit**: fa3f8f3
