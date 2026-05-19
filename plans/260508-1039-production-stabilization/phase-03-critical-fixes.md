# Phase 03: Critical Fixes (C3, C4, H1)

**Priority**: P1
**Effort**: 2 ngày
**Status**: COMPLETED (2026-05-13)
**Depends on**: Phase 01 (cần trace để verify fix), Phase 02 (cần alert kịp phát hiện regression)

## Context

Code review tháng 4 đã flag nhưng chưa fix. Với 1000 tenant production, các issue này không còn lý thuyết:

- **C3**: Race condition `FacebookPageConfig` — **FIXED (pre-2026-05-13)**: Unique index on FacebookPageId + EnsurePageConfigAsync handles race via unique constraint violation catch
- **C4**: Token leak qua query string — **FIXED (pre-2026-05-13)**: Bearer header in Authorization, no access_token in URL
- **H1**: PII (số điện thoại, địa chỉ) plaintext trong log — **COMPLETED (2026-05-13)**: PiiRedactor.MaskPhone/RedactAddress at all call sites + NEW PiiRedactingEnricher.cs (Serilog enricher, defense-in-depth) registered in ObservabilityRegistration + 39 unit tests

## Mục tiêu

Fix 3 issue trên trong 1 batch, deploy sequential với canary.

---

## Task C3: Race condition FacebookPageConfig

### Files to modify
- `Middleware/TenantResolutionMiddleware.cs` (lines 83-122)
- `Services/WebhookProcessor.cs` (lines 232-269)
- `Data/MessengerBotDbContext.cs` — add unique index

### Files to create
- `Migrations/{timestamp}_AddUniqueIndexFacebookPageId.cs`

### Implementation

**Step 1**: Add unique index migration
```csharp
migrationBuilder.CreateIndex(
    name: "IX_FacebookPageConfigs_FacebookPageId_Unique",
    table: "FacebookPageConfigs",
    column: "FacebookPageId",
    unique: true);
```

**Step 2**: Consolidate auto-create logic vào 1 service mới `FacebookPageConfigLookupService`:
- Đã có file `Services/FacebookPageConfigLookupService.cs` — extend chứ không tạo mới
- Method `GetOrCreateAsync(pageId)` dùng `INSERT ... ON CONFLICT (facebook_page_id) DO NOTHING RETURNING *` (Postgres syntax)
- Cả TenantResolutionMiddleware và WebhookProcessor gọi method này

**Step 3**: Remove duplicate logic ở 2 callsite cũ.

### Acceptance
- [ ] Migration apply không lock table > 5s (test trên dump production)
- [ ] Concurrent test: 100 request cùng pageId mới → chỉ 1 row được tạo
- [ ] Trace span `tenant.resolve` cho thấy 0 unique constraint violation sau fix

---

## Task C4: Token leak qua query string

### Files to modify
- `Services/Messenger/MessengerService.cs` (line 44)

### Implementation

**Trước**:
```csharp
var url = $"https://graph.facebook.com/v18.0/me/messages?access_token={pageAccessToken}";
var response = await _httpClient.PostAsync(url, content);
```

**Sau**:
```csharp
var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.facebook.com/v18.0/me/messages")
{
    Content = content
};
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);
var response = await _httpClient.SendAsync(request);
```

**Verify**: Facebook Graph API support Bearer token cho hầu hết endpoint. Test trên 1 tenant canary trước, nếu Facebook reject 401 → fallback dùng `appsecret_proof` thay vì query string.

### Acceptance
- [ ] Send message tới canary tenant thành công
- [ ] Trace span `messenger.send` cho thấy URL không chứa `access_token=`
- [ ] grep log file 24h sau deploy → không thấy token plaintext

---

## Task H1: PII redaction trong log

### Files to modify
- `Services/Observability/PiiRedactor.cs` (tạo mới)
- `StateMachine/Handlers/SalesStateHandlerBase.cs` (line ~110)
- `StateMachine/Handlers/SalesMessageParser.cs` (line ~42)
- `Program.cs` — register Serilog enricher

### Files to create
- `Services/Observability/PiiRedactor.cs`
- `Services/Observability/PiiRedactingEnricher.cs` — Serilog enricher

### Implementation

**Step 1**: `PiiRedactor.Redact(string)` static helper
- Phone: `0912345678` → `0912****78` (giữ 4 đầu + 2 cuối)
- Address: redact toàn bộ (chỉ log `[address]`)
- Email: `abc@def.com` → `a***@def.com`

**Step 2**: Replace direct log:
```csharp
// TRƯỚC
Logger.LogInformation("Loaded remembered phone for PSID: {PSID}: {Phone}", psid, phone);

// SAU
Logger.LogInformation("Loaded remembered phone for PSID: {PSID}: {Phone}",
    PiiRedactor.HashPsid(psid), PiiRedactor.RedactPhone(phone));
```

**Step 3**: Serilog enricher cho safety net — scan message template, redact pattern matches phone/email regex tự động (defense in depth)

### Acceptance
- [ ] Unit test cho `PiiRedactor`: 10 phone format VN khác nhau
- [ ] grep log file 24h sau deploy: 0 plaintext phone, 0 plaintext address
- [ ] Trace tag không chứa raw PSID (đã hash từ Phase 01)

---

## Deploy strategy

Sequential, KHÔNG parallel:

1. **Day 1 morning**: Deploy C4 (token leak) — risk thấp nhất, isolated change
2. **Day 1 afternoon**: Deploy H1 (PII redaction) — backward compatible
3. **Day 2 morning**: Deploy migration C3 (unique index) — apply migration
4. **Day 2 afternoon**: Deploy C3 code change — sau khi migration ổn

Mỗi step:
- Canary 1 tenant trước (chọn tenant volume thấp)
- Monitor 1h, check alert + trace
- Rollout 100% nếu xanh

## Rollback plan

| Task | Rollback |
|------|----------|
| C3 migration | Drop index `IX_FacebookPageConfigs_FacebookPageId_Unique` |
| C3 code | Revert commit, callsite cũ vẫn còn (chưa xóa cho đến khi xanh 1 tuần) |
| C4 | Revert commit, query string version vẫn safe để rollback |
| H1 | Revert commit, không có data migration |

## Risk

| Risk | Mitigation |
|------|------------|
| Migration C3 fail vì có duplicate sẵn | Pre-check: query duplicate trước migration, dedupe manual |
| C4 Facebook reject Bearer token | Test canary, fallback `appsecret_proof` |
| PII redaction quá aggressive làm mất debug info | Log raw vào trace (sampled, secured) thay vì file |
| Serilog enricher chậm (regex per log) | Compile regex static, benchmark < 0.1ms/log |

## Unresolved questions

1. **C3**: Có duplicate FacebookPageId nào tồn tại trong production không? Cần query trước migration.
2. **C4**: Có endpoint Facebook nào KHÔNG support Bearer token? Cần verify với Graph API v18.
3. **H1**: PII có cần xóa khỏi log file đã tồn tại không (retention policy)? Hay chỉ forward-fix?
