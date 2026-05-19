# Phase 10: PDPL 2025/2026 Consent Capture

**Priority**: P1 (compliance)
**Effort**: 1-1.5 ngày
**Status**: Complete
**Depends on**: Phase 05 (CommerceMsgIntent có `ConsentSignal` enum)

---

## Vấn đề

Việt Nam **Luật Bảo vệ Dữ liệu Cá nhân (PDPL)** có hiệu lực từ 2026-01-01 (Nghị định 13/2023/NĐ-CP về Bảo vệ Dữ liệu Cá nhân + Luật mới 2025/2026). Yêu cầu:

1. **Sự đồng ý rõ ràng** trước khi xử lý PII (SĐT, địa chỉ, tên)
2. **Mục đích sử dụng cụ thể** thông báo cho khách
3. **Quyền rút lại** consent bất kỳ lúc nào
4. **Lưu trữ bằng chứng consent** với timestamp, channel, purpose
5. **Penalty**: 2-5% doanh thu năm nếu vi phạm

Code hiện tại:
- `CollectingInfoStateHandler` thu SĐT + địa chỉ tự nhiên qua chat → **KHÔNG có consent capture explicit**
- `Customer` entity có `Phone`, `Address` nhưng **không có `ConsentGivenAt`, `ConsentPurpose`, `ConsentChannel`** fields
- Không có audit log cho consent decision

⇒ **Compliance risk** với 1000 tenant đang lưu trữ PII của customer.

---

## Mục tiêu

1. **Consent capture trong CollectingInfoStateHandler** — hỏi trước khi lưu PII vào DB
2. **Consent audit table** — bằng chứng kiểm toán: tenant, customer, timestamp, channel, purpose, consent_text_shown
3. **Implicit consent fallback** — khách chủ động gửi SĐT trước khi bot hỏi → coi như `Implied`, vẫn lưu audit
4. **Withdraw consent endpoint** — `/api/customers/{psid}/consent/withdraw` cho admin invoke khi khách yêu cầu xóa
5. **Privacy notice** — link đến trang policy mỗi khi hỏi consent lần đầu

---

## Thiết kế

### Consent Audit Entity

```csharp
// Data/Entities/ConsentAuditRecord.cs
public class ConsentAuditRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CustomerPsid { get; set; } = "";
    public ConsentDecision Decision { get; set; }  // Given, Refused, Withdrawn, Implied
    public string Purpose { get; set; } = "";       // "order_fulfillment", "marketing_followup"
    public string Channel { get; set; } = "";       // "messenger"
    public string ConsentTextShown { get; set; } = "";  // exact wording shown to customer
    public DateTime CreatedAt { get; set; }
    public string? WithdrawnReason { get; set; }    // if Decision = Withdrawn
}

public enum ConsentDecision { Given, Refused, Implied, Withdrawn }
```

Migration: thêm `consent_audit` table với index `(tenant_id, customer_psid, created_at DESC)`.

### Customer entity extension

```csharp
// Customer.cs — thêm fields
public DateTime? ConsentGivenAt { get; set; }
public string? ConsentPurposes { get; set; }  // CSV: "order_fulfillment,marketing"
public bool MarketingConsentGiven { get; set; }
```

### Consent Flow trong CollectingInfoStateHandler

```
User: "0901xxxxxxx, 123 Lê Lợi Q1"  (chủ động cung cấp PII trước khi hỏi)
Bot detects: hasPhone=true, hasAddress=true, consent=NotAsked
   → Save ConsentAuditRecord(Decision=Implied, Purpose="order_fulfillment")
   → Lưu PII vào Customer
   → Bot reply confirm order + thêm dòng:
     "Em ghi nhận thông tin chị để giao hàng và liên hệ về đơn nha. 
      Em chỉ dùng cho đơn lần này, không gửi marketing nếu chị không đồng ý.
      Chính sách bảo mật: [link]"
```

```
User: "tạo đơn cho em"  (chưa cung cấp PII)
Bot detects: missing SĐT + address, consent=NotAsked
   → Reply: "Để lên đơn em xin chị SĐT và địa chỉ giao hàng nha. 
            Thông tin chỉ dùng cho đơn này, không chia sẻ bên thứ 3. OK chị?"
   → SetData("pendingConsentQuestion", "explicit_consent")
   → Wait response

User: "OK em"
   → Save ConsentAuditRecord(Decision=Given, Purpose="order_fulfillment")
   → Bot: "Dạ chị gửi em SĐT và địa chỉ ạ."

User: "không, tôi không muốn"
   → Save ConsentAuditRecord(Decision=Refused, Purpose="order_fulfillment")
   → Bot: "Dạ em hiểu rồi ạ. Em sẽ chuyển nhân viên hỗ trợ trực tiếp cho chị."
   → Escalate to human handoff
```

### ConsentService

```csharp
public interface IConsentService
{
    Task RecordConsentAsync(
        Guid tenantId, string customerPsid, ConsentDecision decision,
        string purpose, string channel, string consentTextShown,
        CancellationToken ct = default);

    Task<bool> HasValidConsentAsync(
        Guid tenantId, string customerPsid, string purpose,
        CancellationToken ct = default);

    Task WithdrawConsentAsync(
        Guid tenantId, string customerPsid, string? reason,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConsentAuditRecord>> GetAuditTrailAsync(
        Guid tenantId, string customerPsid,
        CancellationToken ct = default);
}
```

### Privacy Notice URL

Tenant-configurable: `TenantSettings.PrivacyPolicyUrl` (đã có hoặc thêm mới). Default fallback: `https://{tenant_domain}/privacy` hoặc link chung của platform.

Consent text mẫu (tenant override được):
```
"Bên em chỉ dùng thông tin này để giao đơn và liên hệ về đơn hiện tại.
Không chia sẻ với bên thứ 3 ngoài đơn vị vận chuyển.
Chính sách bảo mật: {privacyPolicyUrl}
Chị đồng ý chứ ạ?"
```

### Withdraw Endpoint

```csharp
// Endpoints/CustomerConsentEndpoints.cs
app.MapPost("/api/admin/customers/{psid}/consent/withdraw",
    async (string psid, WithdrawConsentRequest req, IConsentService service, ITenantContext tenant) =>
    {
        await service.WithdrawConsentAsync(tenant.TenantId, psid, req.Reason);
        // Trigger: cascade delete or anonymize PII per PDPL Article X
        return Results.Ok();
    })
    .RequireAuthorization("AdminOnly");
```

### Anonymization on Withdraw

Khi consent withdrawn:
- `Customer.Phone` → `"WITHDRAWN_{psid_hash}"`
- `Customer.Address` → `null`
- Conversation history giữ nguyên (legal retention) nhưng PII fields trong `StateContext.Data` được scrubbed

Note: PDPL cho phép retention cho legal/dispute resolution purposes — không phải xóa hoàn toàn ngay.

---

## Files cần tạo

- `Data/Entities/ConsentAuditRecord.cs`
- `Data/Migrations/{date}_AddConsentAudit.cs`
- `Services/Consent/IConsentService.cs`
- `Services/Consent/ConsentService.cs`
- `Endpoints/CustomerConsentEndpoints.cs`
- `Configuration/ConsentOptions.cs` — default purpose list, retention days

## Files cần sửa

- `Data/Entities/Customer.cs` — thêm ConsentGivenAt, ConsentPurposes, MarketingConsentGiven
- `StateMachine/Handlers/CollectingInfoStateHandler.cs` — consent capture flow
- `Services/Sales/Contact/ContactConfirmationFlow.cs` — check `HasValidConsentAsync` trước khi confirm
- `Services/Customers/CustomerService.cs` (nếu tồn tại) — call `RecordConsentAsync` khi update PII
- `Configuration/ServiceRegistration/SalesPipelineRegistration.cs` — register IConsentService
- `Data/MessengerBotDbContext.cs` — DbSet<ConsentAuditRecord>

---

## Implementation Steps

### Step 1: Entity + migration (0.25 ngày)

```bash
dotnet ef migrations add AddConsentAudit --project src/MessengerWebhook
```

Verify `consent_audit` table có `tenant_id` index (multi-tenant isolation).

### Step 2: ConsentService implementation (0.25 ngày)

CRUD operations. Audit trail query support.

### Step 3: Tích hợp CollectingInfoStateHandler (0.5 ngày)

3 paths:
- Path A: PII đã có trong message → Implied consent
- Path B: Cần hỏi → set pendingConsentQuestion, return prompt
- Path C: User phản hồi consent question → Given/Refused

Reference flow trong Thiết kế trên.

### Step 4: Withdraw endpoint + anonymization (0.25 ngày)

Admin endpoint với role check. Anonymize trong transaction.

### Step 5: Tests (0.25 ngày)

Unit test:
- Implied consent khi user chủ động gửi PII
- Explicit consent question flow (Given/Refused)
- Withdraw anonymizes PII
- Audit trail query returns correct decisions

Integration test: end-to-end conversation từ chào → consent → order.

---

## Todo

- [ ] Tạo ConsentAuditRecord entity + migration
- [ ] Extend Customer entity với consent fields
- [ ] Tạo IConsentService + ConsentService
- [ ] Tích hợp consent flow vào CollectingInfoStateHandler
- [ ] Cập nhật ContactConfirmationFlow check consent trước confirm
- [ ] Tạo /admin/customers/{psid}/consent/withdraw endpoint
- [ ] Anonymization logic on withdraw
- [ ] Privacy policy URL config per tenant
- [ ] Unit test 3 consent paths
- [ ] Integration test end-to-end flow
- [ ] Build + tests pass
- [ ] Update docs/data-privacy-pdpl.md với compliance statement

---

## Success Criteria

- 100% new PII storage có corresponding ConsentAuditRecord
- Withdraw endpoint working, anonymization verified
- Audit trail query returns full history per customer
- Privacy notice text shown to khách trước khi xin PII explicit
- 0 regression trong order completion rate (consent flow không block conversion)

---

## Risk

- **UX friction**: Hỏi consent có thể giảm conversion. Mitigation: consent text NGẮN, natural language, không legal-sounding. A/B test wording trước rollout 100%
- **Implied consent dispute**: Khách claim "tôi không biết". Mitigation: Privacy notice luôn link kèm sau khi nhận PII implied + audit trail có exact text shown
- **Migration với 1000 tenant existing customers**: Customers cũ đã có PII nhưng không có consent record. Mitigation: 
  - Tạo migration backfill: tất cả existing customers gán `Decision=Implied`, `Purpose=legacy_grandfathered`, `CreatedAt=migration_date`
  - Hoặc soft notification first conversation sau migration: "Em vẫn giữ thông tin liên hệ của chị từ trước. Chị có muốn tiếp tục dùng cho đơn lần này không?"
- **Multi-tenant policy variation**: Mỗi tenant có policy riêng. Mitigation: `TenantSettings.PrivacyPolicyUrl` + `ConsentTextTemplate` configurable

---

## Compliance References

- Nghị định 13/2023/NĐ-CP về Bảo vệ Dữ liệu Cá nhân (hiệu lực 2023-07-01)
- Luật Bảo vệ Dữ liệu Cá nhân (dự kiến 2026 — verify status với legal team)
- ISO/IEC 27701:2019 — privacy information management
- (Optional) GDPR Article 7 — nếu có EU customer trong tương lai

## Unresolved questions

1. Privacy policy URL: dùng platform chung hay mỗi tenant tự cung cấp?
2. Marketing consent: tách riêng khỏi `order_fulfillment` consent (PDPL yêu cầu purpose-specific) — implement luôn hay defer phase sau?
3. Backfill existing customers: `Implied` legacy_grandfathered có đủ legal cover không? Cần verify với legal counsel
4. Retention period sau withdraw: PDPL yêu cầu xóa hoặc anonymize trong bao nhiêu ngày? (đề xuất 30 ngày grace để xử lý dispute)
