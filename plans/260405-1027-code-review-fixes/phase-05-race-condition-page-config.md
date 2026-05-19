---
phase: 05
title: "C3: Fix Race Condition in FacebookPageConfig Auto-Creation"
priority: P1 (Critical)
status: pending
depends_on: none
---

## Overview
Consolidate duplicate auto-creation logic for `FacebookPageConfig` into a single idempotent service with distributed lock.

## Files to Modify
- `src/MessengerWebhook/Services/FacebookPageConfigService.cs` (new, or update existing)
- `src/MessengerWebhook/Middleware/TenantResolutionMiddleware.cs` (lines 83-122)
- `src/MessengerWebhook/Services/WebhookProcessor.cs` (lines 232-269, method `TryAdoptUnknownDevelopmentPageAsync`)
- `src/MessengerWebhook/Data/MessengerBotDbContext.cs` (add unique index if not exists)

## Implementation Steps

1. **Add unique constraint in migration**
   - Add `builder.HasIndex(x => x.FacebookPageId).IsUnique()` to `FacebookPageConfigs` configuration
   - Create migration: `dotnet ef migrations add AddUniqueIndex_FacebookPageConfigs_FacebookPageId`

2. **Create consolidated `FacebookPageConfigLookupService`**
   - File: `src/MessengerWebhook/Services/FacebookPageConfigLookupService.cs` (new)
   - Method: `EnsurePageConfigAsync(string facebookPageId, CancellationToken ct)`
   - Implementation:
     ```sql
     -- Use raw SQL with ON CONFLICT DO NOTHING or EF Core upsert pattern
     INSERT INTO "FacebookPageConfigs" (...) VALUES (...)
     ON CONFLICT ("FacebookPageId") DO NOTHING
     RETURNING "Id"
     ```
   - If no existing page config and insert fails silently, re-query to get the one created by concurrent request

3. **Replace middleware auto-creation**
   - In `TenantResolutionMiddleware.cs`: replace lines 83-122 with call to `EnsurePageConfigAsync()`
   - Remove duplicated insert logic

4. **Replace webhook processor auto-creation**
   - In `WebhookProcessor.cs`: replace `TryAdoptUnknownDevelopmentPageAsync` with call to same `EnsurePageConfigAsync()`
   - Remove duplicated insert logic

5. **Handle page access token fallback**
   - When auto-creating with no page token, use global fallback token but mark configuration as `RequiresTokenUpdate = true`
   - Add admin warning for pages that need token configuration

## Success Criteria
- Only one code path for page config creation
- Concurrent requests cannot create duplicate records (unique index enforces)
- No data loss or silent failures
- `dotnet build` succeeds, tests pass

## Risk Assessment
- **Likelihood:** Medium - migration adds unique index to existing table
- **Impact:** High if existing duplicates violate new unique constraint
- **Mitigation:** Before migration, run cleanup: deduplicate existing records, keep most recent

## Rollback
Revert migration (EF Core supports down). Restore previous auto-creation code.
