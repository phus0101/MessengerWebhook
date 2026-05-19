# Phase 06: RAG Metadata Enrichment

**Priority**: P2  
**Effort**: 1-2 ngày  
**Status**: Complete  
**Depends on**: None (độc lập, có thể chạy song song với các phase khác)

---

## Vấn đề

Research doc: *"Metadata tối thiểu nên có `sku`, `category`, `locale`, `price_effective_date`, `inventory_region`, `policy_version`, `source_url`, `brand`, `channel_visibility`."*

*"Filter metadata trước rồi mới semantic search/rerank; đừng semantic search trên toàn bộ kho tài liệu."*

Hiện tại `PineconeVectorService.UpsertProductAsync` nhận `Dictionary<string, object> metadata` — caller quyết định fields. Cần kiểm tra `KnowledgeImportService` và product embedding pipeline để xác định metadata nào đang được populate.

---

## Mục tiêu

1. **Thêm missing metadata fields** vào upsert pipeline
2. **Dùng metadata filter trong search** — filter `channel_visibility`, `policy_version` trước semantic search
3. **Không break existing data** — new fields nullable/optional, data cũ vẫn hoạt động
4. **Document metadata schema** rõ ràng để team biết chuẩn

---

## Metadata Schema Target

```csharp
public static class VectorMetadataKeys
{
    // Đã có (giả định)
    public const string Sku = "sku";
    public const string Category = "category";
    public const string Brand = "brand";
    public const string TenantId = "tenant_id";

    // Cần thêm — research-mandated 9 fields
    public const string Locale = "locale";                    // "vi", "en"
    public const string PriceEffectiveDate = "price_eff_date"; // "2026-05-01"
    public const string InventoryRegion = "inventory_region"; // "HN", "HCM", "DN", "ALL"
    public const string PolicyVersion = "policy_version";     // "v2026Q2"
    public const string SourceUrl = "source_url";             // URL tài liệu gốc
    public const string ChannelVisibility = "channel_visibility"; // "messenger", "web", "all"
    public const string ContentType = "content_type";         // "product", "policy", "faq"
}
```

### Filter Strategy trong Search

```csharp
// PineconeVectorService.SearchAsync — thêm metadata filter dùng Pinecone.Metadata (v2.0.0)
// Helper PineconeFilterBuilder (xem Phase 06 Step 6 dưới)
var filter = PineconeFilterBuilder.And(
    PineconeFilterBuilder.Eq(VectorMetadataKeys.TenantId, tenantId),
    // $or để vector cũ thiếu channel_visibility vẫn được include
    PineconeFilterBuilder.Or(
        PineconeFilterBuilder.In(VectorMetadataKeys.ChannelVisibility, new[] { "messenger", "all" }),
        PineconeFilterBuilder.Exists(VectorMetadataKeys.ChannelVisibility, exists: false)
    )
);

// Khi search policy/FAQ: filter thêm policy_version mới nhất
if (contentType == "policy")
    filter = PineconeFilterBuilder.And(filter,
        PineconeFilterBuilder.Eq(VectorMetadataKeys.PolicyVersion, currentPolicyVersion));
```

### Step 6: PineconeFilterBuilder helper

```csharp
// Services/VectorSearch/PineconeFilterBuilder.cs
using Pinecone;

public static class PineconeFilterBuilder
{
    public static Metadata Eq(string field, object value)
        => new() { [field] = ConvertValue(value) };

    public static Metadata In(string field, IEnumerable<string> values)
        => new() { [field] = new Metadata { ["$in"] = values.ToArray() } };

    public static Metadata Exists(string field, bool exists = true)
        => new() { [field] = new Metadata { ["$exists"] = exists } };

    public static Metadata And(params Metadata[] clauses)
        => new() { ["$and"] = clauses };

    public static Metadata Or(params Metadata[] clauses)
        => new() { ["$or"] = clauses };

    private static MetadataValue ConvertValue(object value) => value switch
    {
        string s => s,
        bool b => b,
        int i => (double)i,
        long l => (double)l,
        double d => d,
        _ => value.ToString() ?? string.Empty
    };
}
```

**Note**: Code hiện tại `PineconeVectorService.ConvertToMetadata` chỉ làm equality match flat dict — không hỗ trợ `$in`/`$eq`/`$exists`. `PineconeFilterBuilder` thay thế cho complex filter use cases.

---

## Files cần kiểm tra trước khi implement

```bash
# Tìm tất cả chỗ upsert vector
grep -rn "UpsertProductAsync\|UpsertAsync" src/ --include="*.cs"
grep -rn "metadata\[" src/MessengerWebhook/Services/Knowledge/ --include="*.cs"
grep -rn "metadata\[" src/MessengerWebhook/Services/VectorSearch/ --include="*.cs"
```

---

## Files cần tạo

- `Services/VectorSearch/VectorMetadataKeys.cs` — constants cho metadata keys

## Files cần sửa

- `Services/Knowledge/KnowledgeImportService.cs` — populate new metadata fields khi import
- `Services/VectorSearch/PineconeVectorService.cs` — thêm metadata filter vào search
- `Services/VectorSearch/Models/` — nếu cần typed model cho metadata

---

## Implementation Steps

### Step 1: Audit hiện trạng (0.25 ngày)

```bash
# Xem KnowledgeImportService đang populate những field nào
cat src/MessengerWebhook/Services/Knowledge/KnowledgeImportService.cs

# Xem PineconeVectorService.SearchAsync filter hiện tại
grep -A 20 "SearchAsync" src/MessengerWebhook/Services/VectorSearch/PineconeVectorService.cs
```

Xác định gap giữa schema hiện tại và target.

### Step 2: Tạo VectorMetadataKeys constants (0.25 ngày)

```csharp
// Services/VectorSearch/VectorMetadataKeys.cs
public static class VectorMetadataKeys
{
    public const string Sku = "sku";
    // ... tất cả fields
}
```

Refactor KnowledgeImportService + PineconeVectorService dùng constants thay vì magic strings.

### Step 3: Populate new fields trong KnowledgeImportService (0.5 ngày)

Khi import document, thêm:
```csharp
metadata[VectorMetadataKeys.Locale] = document.Locale ?? "vi";
metadata[VectorMetadataKeys.ChannelVisibility] = document.ChannelVisibility ?? "all";
metadata[VectorMetadataKeys.ContentType] = document.ContentType; // "product"/"policy"/"faq"
metadata[VectorMetadataKeys.PolicyVersion] = document.PolicyVersion ?? "v1";
metadata[VectorMetadataKeys.PriceEffectiveDate] = document.PriceEffectiveDate?.ToString("yyyy-MM-dd");
```

**Existing data**: New fields = null cho vectors đã upsert. Filter phải handle null gracefully (dùng `$exists` check hoặc skip filter nếu field null).

### Step 4: Metadata filter trong SearchAsync (0.5 ngày)

```csharp
// PineconeVectorService.SearchAsync
// Thêm filter cho tenant + channel visibility
var filter = BuildSearchFilter(tenantId, channelVisibility: "messenger");

var result = await index.QueryAsync(new QueryRequest
{
    Vector = queryEmbedding,
    TopK = topK,
    Filter = filter,
    Namespace = tenantNamespace
});
```

Cần `SearchOptions` parameter để caller có thể pass thêm filters:

```csharp
public record VectorSearchOptions
{
    public string? ContentType { get; init; }    // filter theo loại content
    public string? PolicyVersion { get; init; }  // filter theo version
    public string? ChannelVisibility { get; init; } = "all";
    public string? Locale { get; init; }
}
```

### Step 5: Wire SearchOptions vào RAGService (0.25 ngày)

`RAGService` → `HybridSearchService` → `PineconeVectorService`:
- Khi query context type = policy → pass `ContentType = "policy"`, `PolicyVersion = currentVersion`
- Khi query context type = product → pass `ContentType = "product"`

---

## Todo

- [ ] Audit KnowledgeImportService metadata fields hiện tại
- [ ] Audit PineconeVectorService search filter hiện tại
- [ ] Tạo VectorMetadataKeys constants
- [ ] Refactor existing code dùng constants
- [ ] Thêm new metadata fields vào KnowledgeImportService
- [ ] Tạo VectorSearchOptions record
- [ ] Thêm filter vào PineconeVectorService.SearchAsync
- [ ] Wire SearchOptions qua RAGService → HybridSearchService
- [ ] Test: import document mới có đủ metadata
- [ ] Test: search filter đúng channel_visibility
- [ ] Build + tests pass

---

## Success Criteria

- `KnowledgeImportService` populate đủ 8 metadata fields cho document mới
- `PineconeVectorService.SearchAsync` có filter `channel_visibility` + `tenant_id`
- Không có magic string metadata keys (tất cả dùng `VectorMetadataKeys.xxx`)
- Existing vectors (không có new fields) vẫn được trả về trong search (null filter graceful)

---

## Risk

- **Pinecone filter syntax**: Pinecone metadata filter dùng `$eq`, `$in`, `$exists` — verify syntax với Pinecone.Client v2.0.0 SDK
- **Existing data không có new fields**: Search filter `channel_visibility = "messenger"` sẽ miss vectors cũ chưa có field này. Giải pháp: filter `channel_visibility IN ["messenger", null]` hoặc không filter channel_visibility cho records cũ
- **Re-index required**: Vectors đã upsert không có new metadata. Không cần re-index ngay — chỉ new upserts có đủ fields. Lên lịch re-index toàn bộ trong maintenance window riêng
