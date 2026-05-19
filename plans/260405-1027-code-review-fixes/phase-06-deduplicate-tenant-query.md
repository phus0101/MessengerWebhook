---
phase: 06
title: "M1: Deduplicate Tenant Resolution Queries"
priority: P3 (Medium)
status: pending
depends_on: 05
---

## Overview
Propagate resolved tenant context from middleware to WebhookProcessor instead of re-querying from database.

## Files to Modify
- `src/MessengerWebhook/Middleware/TenantResolutionMiddleware.cs`
- `src/MessengerWebhook/Services/WebhookProcessor.cs` (method `InitializeTenantContextAsync`)
- `src/MessengerWebhook/Models/TenantContext.cs` (add `FacebookPageConfig` property if needed)

## Implementation Steps

1. **Extend `TenantContext` to carry cached config**
   - Add property `FacebookPageConfig? ResolvedPageConfig { get; set; }` to tenant context
   - Middleware already queries this during tenant resolution - store result in context

2. **Update middleware**
   - After resolving tenant, attach the `FacebookPageConfig` to `TenantContext`
   - Register in HttpContext.Items so downstream services can access

3. **Update `WebhookProcessor.InitializeTenantContextAsync`**
   - Check if `TenantContext.ResolvedPageConfig` is already set
   - If set, skip database query entirely
   - Only query DB if middleware did not resolve (edge case: direct processor invocation outside HTTP pipeline)

4. **Remove duplicate query code**
   - Delete the `FacebookPageConfigs` query from `InitializeTenantContextAsync`
   - Keep it only as fallback path with `[Obsolete]` comment

## Success Criteria
- Single database query per request (in middleware), not two
- Same behavior for requests through middleware AND direct processor invocation
- No regression in tenant isolation

## Risk Assessment
- **Likelihood:** Low
- **Impact:** Low - purely query reduction, no behavior change
- **Mitigation:** Verify with SQL profiling that query count reduced

## Rollback
Revert commit. Duplicate queries are performance issue, not correctness issue.
