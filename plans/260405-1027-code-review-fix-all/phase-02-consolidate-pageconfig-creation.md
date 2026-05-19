# Phase 2: Consolidate FacebookPageConfig Auto-Creation (C3)

## Overview
- Priority: Critical
- Current status: Not started
- Effort: 1h
- Issue: C3 — Race condition in FacebookPageConfig auto-creation across middleware and webhook processor

## Problem
Both `TenantResolutionMiddleware` (lines 83-122) and `WebhookProcessor` (lines 232-269) contain identical `TryAdoptUnknownDevelopmentPageAsync` logic. When concurrent requests arrive from an unknown page in development, both paths attempt to INSERT the same `FacebookPageId` simultaneously → duplicate key or data inconsistency.

## Context Links
- `src/MessengerWebhook/Middleware/TenantResolutionMiddleware.cs:83-122`
- `src/MessengerWebhook/Services/WebhookProcessor.cs:232-269`
- `src/MessengerWebhook/Data/Entities/FacebookPageConfig.cs`

## Architecture

Consolidation strategy — single service with EF Core `ExecuteMerge` (ON CONFLICT DO NOTHING):

```
TenantResolutionMiddleware (webhook POST)
  └── PageConfigAdoptionService.TryAdoptAsync(pageId)
        ├── Acquire distributed lock OR use EF Core raw SQL
        ├── Check if page exists (already created by concurrent request)
        ├── INSERT ... ON CONFLICT DO NOTHING
        └── Return config

WebhookProcessor.ProcessAsync()
  └── Uses tenantContext already set by middleware → no duplicate query
```

Since the middleware runs before the webhook endpoint, and the WebhookProcessor reads from the channel (after the HTTP request), the middleware already resolved the tenant. The WebhookProcessor's duplicate `InitializeTenantContextAsync` call should just read from `ITenantContext` instead of re-querying the DB.

## Implementation Steps

### Step 1: Remove TryAdoptUnknownDevelopmentPageAsync from WebhookProcessor

Delete `TryAdoptUnknownDevelopmentPageAsync` from `WebhookProcessor.cs` — it is never reached in production since the middleware already handles adoption. The middleware sets `ITenantContext` before the request pipeline completes.

### Step 2: Add ON CONFLICT DO NOTHING to middleware

In `TenantResolutionMiddleware.TryAdoptUnknownDevelopmentPageAsync`, wrap the INSERT with a re-check:

```csharp
// Re-check under potential race
var existing = await dbContext.FacebookPageConfigs
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(x => x.FacebookPageId == pageId, cancellationToken);
if (existing != null) return existing; // Another request already created it

// Proceed with insert
dbContext.FacebookPageConfigs.Add(adoptedConfig);
try
{
    await dbContext.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException) when (unique constraint violated)
{
    // Another concurrent request won — fetch and return
    return await dbContext.FacebookPageConfigs
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.FacebookPageId == pageId, cancellationToken);
}
```

### Step 3: Add unique index migration

Create new EF migration adding unique index on `FacebookPageConfigs.FacebookPageId`:
```bash
dotnet ef migrations add AddUniqueIndexToFacebookPageId --project src/MessengerWebhook
```
Migration should use:
```sql
CREATE UNIQUE INDEX "IX_FacebookPageConfigs_FacebookPageId" ON "FacebookPageConfigs" ("FacebookPageId");
```

### Step 4: Update WebhookProcessor to skip redundant query

In `WebhookProcessor.InitializeTenantContextAsync`, check `ITenantContext` first:
```csharp
if (_tenantContext.TenantId != null)
    return; // Already resolved by middleware
```

## Related Code Files

**To modify:**
- `src/MessengerWebhook/Middleware/TenantResolutionMiddleware.cs` (add re-check + exception handling)
- `src/MessengerWebhook/Services/WebhookProcessor.cs` (remove TryAdoptUnknownDevelopmentPageAsync, skip redundant query)

**To create:**
- EF migration: `AddUniqueIndexToFacebookPageId`

## Todo List

- [ ] Add re-check pattern to TenantResolutionMiddleware
- [ ] Add DbUpdateException handling for race tolerance
- [ ] Remove TryAdoptUnknownDevelopmentPageAsync from WebhookProcessor
- [ ] Create EF migration for unique index on FacebookPageId
- [ ] Verify dotnet build succeeds
- [ ] Verify IntegrationTests.WebhookTests still pass

## Success Criteria

- Only one code path creates FacebookPageConfig (middleware)
- Unique index prevents duplicate FacebookPageId
- Concurrent requests don't throw — second request fetches the first's record
- WebhookProcessor skips redundant tenant query when already resolved

## Risk Assessment

**Medium risk.** Adding a unique index could fail if duplicate FacebookPageId already exists. Mitigation:
- Migration should first check for and deduplicate existing rows
- Use `Delete duplicates before adding index` pattern in migration
- Index creation should be `CREATE UNIQUE INDEX IF NOT EXISTS`
