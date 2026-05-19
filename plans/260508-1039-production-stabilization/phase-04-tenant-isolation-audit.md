# Phase 04: Tenant Isolation Audit

**Priority**: P0 (severity) — risk thấp xảy ra, nhưng impact thảm họa
**Effort**: 2-3 ngày
**Status**: Complete (2026-05-13)
**Depends on**: Phase 01 (cần trace để verify runtime)

## Context

1000 tenant trong shared schema database. Tenancy isolation chỉ qua `tenant_id` filter ở application layer. **Một query thiếu filter = data leak giữa các shop = thảm họa pháp lý + reputation.**

Chưa có audit toàn diện kể từ khi tenant_id được introduce.

## Mục tiêu

1. Liệt kê **mọi entity** có `TenantId` column
2. Verify **mọi LINQ query** trên entity đó có filter (qua EF query filter hoặc explicit `Where`)
3. Verify **mọi raw SQL/EF.SqlQuery** có parameter tenant_id
4. Verify **cache key** của Redis có chứa tenant_id (đã làm partial — kiểm lại)
5. Verify **Pinecone namespace/filter** isolate theo tenant
6. Test runtime: fake 2 tenant, gửi request, xác nhận data không leak

## Files to read (audit scope)

- `Data/Entities/*.cs` — tìm entity có `TenantId`
- `Data/MessengerBotDbContext.cs` — EF query filters
- `Data/Repositories/*.cs` — repository queries
- `Services/**/*.cs` — direct DbContext access
- `Services/Cache/CacheKeyGenerator.cs` — cache key có tenant
- `Services/VectorSearch/PineconeVectorService.cs` — namespace/filter

## Files to create

- `plans/reports/audit-260520-tenant-isolation-{slug}.md` — báo cáo findings
- `tests/MessengerWebhook.IntegrationTests/TenantIsolationTests.cs` — automated test

## Implementation steps

### Step 1: Inventory entities (0.5 ngày)

Grep tất cả class có property `TenantId`:
```bash
grep -r "TenantId" src/MessengerWebhook/Data/Entities/ --include="*.cs"
```

Tạo bảng:
| Entity | Has TenantId | Has Global Query Filter | Status |
|--------|--------------|-------------------------|--------|
| Product | Y | ? | Audit |
| ConversationSession | Y | ? | Audit |
| ... | | | |

### Step 2: Verify EF Global Query Filters (0.5 ngày)

Trong `MessengerBotDbContext.OnModelCreating`:
- Mọi entity multi-tenant phải có:
  ```csharp
  modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.CurrentTenantId);
  ```
- Liệt kê entity thiếu, thêm filter

**Cẩn thận**: query filter có thể bị bypass bởi `IgnoreQueryFilters()`. Grep tìm callsite dùng method này — phải có lý do chính đáng + comment.

### Step 3: Audit repository + service queries (1 ngày)

Pattern cần kiểm:
- `_dbContext.Products.Where(...)` — có chắc query filter active?
- `_dbContext.Database.SqlQuery<T>(...)` — raw SQL, BẮT BUỘC filter tay
- `_dbContext.Set<T>().FromSqlRaw(...)` — tương tự
- Background services / hosted services — không có HTTP scope, `_tenantContext` có resolve không?

Output: danh sách query suspicious + fix recommendation.

### Step 4: Cache + Vector store audit (0.5 ngày)

- `CacheKeyGenerator` — verify mọi method tạo key có include `tenantId` parameter
- Embedding cache: key format `emb:sha256(text)` — **cố ý** không tenant-scoped (text giống nhau giữa tenant cho phép share). OK nếu embedding deterministic + không chứa tenant data.
- Result cache: phải có tenant trong key. Verify.
- Pinecone: dùng `namespace=tenant_id` hay metadata filter `tenant_id`?

### Step 5: Runtime test (0.5 ngày)

`TenantIsolationTests.cs`:
- Setup 2 tenant A, B
- Tenant A tạo 5 product, 3 conversation
- Tenant B tạo 4 product, 2 conversation
- Login tenant B, query products → expect 4, không thấy của A
- Webhook PSID của A đến endpoint của B → expect 404 hoặc 0 result, không crash hoặc trả data A
- Cache poisoning test: tenant A request, tenant B request cùng query — verify result không lẫn

### Step 6: Add CI guardrail (0.5 ngày)

- Custom Roslyn analyzer hoặc archunit-style test:
  - Fail build nếu có `IgnoreQueryFilters()` không có `// ALLOW: <reason>` comment
  - Fail build nếu raw SQL không reference `TenantId` parameter

## Acceptance criteria

- [ ] 100% entity có TenantId được liệt kê + có global query filter
- [ ] 0 callsite `IgnoreQueryFilters()` không có justification comment
- [ ] 0 raw SQL thiếu tenant filter
- [ ] Cache key audit pass: result/response cache có tenant_id, embedding cache có lý do explicit
- [ ] Pinecone audit: tenant filter active mọi search call
- [ ] Integration test `TenantIsolationTests` pass
- [ ] CI guardrail block PR vi phạm

## Findings format (báo cáo)

```markdown
## Findings

### CRITICAL: <số lượng>
- File:line — entity X queryable không có filter
- ...

### HIGH: <số lượng>
- File:line — raw SQL thiếu tenant
- ...

### MEDIUM: <số lượng>
- File:line — query filter có thể bị bypass

### Verified clean: <số lượng>
- ...
```

## Risk

| Risk | Mitigation |
|------|------------|
| Audit phát hiện leak đã xảy ra production | Postmortem ngay, notify affected tenant nếu xác định, GDPR-style disclosure |
| Thêm query filter làm break query hiện tại | Test toàn bộ test suite, canary deploy |
| Background service không có tenant context | Refactor: pass tenant_id explicit vào method |
| Pinecone migration namespace tốn kost | Dùng metadata filter thay vì namespace nếu chi phí cao |

## Rollback

- Thêm query filter là backward-compatible (chỉ thắt chặt, không nới lỏng)
- Nếu break: revert commit, query filter được optional qua `[NotMultiTenant]` attribute

## Unresolved questions

1. **Có incident leak nào đã xảy ra chưa?** — kiểm log lịch sử, customer report
2. **Background services** dùng tenant context như thế nào? Cần đọc code:
   - `BackgroundServices/*.cs`
   - LiveCommentAutomationService
   - ProductEmbeddingPipeline
3. **Admin endpoints** — admin được phép cross-tenant query, làm sao distinguish?
4. **Test coverage**: có test nào hiện tại chạy với 2+ tenant chưa?
5. **Pinecone**: hiện tại dùng namespace hay metadata filter? Nếu chuyển sang namespace = migration data
