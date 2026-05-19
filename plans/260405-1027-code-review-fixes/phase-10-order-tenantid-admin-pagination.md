---
phase: 10
title: "M3: Order TenantId + M4: Admin List Pagination"
priority: P3 (Medium)
status: pending
depends_on: 05
---

## Overview
Add direct TenantId column to Order entity and add pagination to admin list endpoints.

## Files to Modify
- `src/MessengerWebhook/Models/Order.cs`
- `src/MessengerWebhook/Data/MessengerBotDbContext.cs`
- `src/MessengerWebhook/Features/Admin/AdminOperationsEndpointExtensions.cs`

## Implementation Steps

### M3: Order TenantId

1. **Update Order entity**
   - Add `public Guid TenantId { get; set; }` property
   - Implement `ITenantOwnedEntity` interface: `public Guid TenantId { get; set; }`
   - EF Core global query filter will auto-include this entity

2. **Create migration**
   - `dotnet ef migrations add AddTenantId_ToOrders`
   - Set default TenantId for existing orders from their associated Session:
     ```sql
     UPDATE "Orders" o SET "TenantId" = s."TenantId"
     FROM "Sessions" s WHERE o."SessionId" = s."Id"
     ```

3. **Update Order creation code**
   - Ensure all Order creation paths set TenantId from current tenant context
   - Should be automatic via EF Core interceptor or DI context, but verify

### M4: Admin List Pagination

1. **Add pagination parameters**
   - Update admin endpoints: `/admin/api/draft-orders`, `/admin/api/customers`, `/admin/api/orders`
   - Add query parameters: `page` (default 1), `pageSize` (default 20, max 100)
   - Validate pageSize: clamp to 100 if exceeds, default to 20 if zero/negative

2. **Update queries**
   - Add `.Skip((page - 1) * pageSize).Take(pageSize)` to all list queries
   - Return pagination metadata in response:
     ```json
     { "items": [...], "totalCount": 1234, "page": 1, "pageSize": 20, "totalPages": 62 }
     ```

3. **Create pagination response wrapper**
   - File: `src/MessengerWebhook/Models/PaginatedResponse.cs` (new)
   - Generic response type for all paginated endpoints

## Success Criteria
- Order entity has TenantId column with correct values for existing data
- Admin list endpoints support pagination with metadata
- Default page size prevents loading 10,000 records
- `dotnet build` succeeds, tests pass

## Risk Assessment
- **Likelihood:** Medium for M3 (data migration), Low for M4
- **Impact:** Medium if migration fails for Orders with missing Session
- **Mitigation:** Migration script must handle orphaned orders (set to default or delete)

## Rollback
M3: Revert migration may fail if data depends on TenantId - need cleanup script.
M4: Safe to revert, pagination is additive feature.
