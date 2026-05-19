# Refactor Plan — 4 câu hỏi review đã resolve

**Date**: 2026-05-17 12:24 ICT
**Plan**: `plans/260517-1156-sales-copilot-research-refactor/`

---

## 1. TargetFramework: net8.0 → net9.0 ✅ DONE

### Hiện trạng trước
| Project | Target | Note |
|---|---|---|
| `src/MessengerWebhook` | net8.0 | Bị mâu thuẫn với CLAUDE.md ".NET SDK 9.0.200+" |
| `tests/UnitTests` | net9.0 | Đã upgrade trước đó |
| `tests/IntegrationTests` | net8.0 | Bị bỏ sót |

### Action đã thực hiện
- Edit `MessengerWebhook.csproj` line 4: `net8.0` → `net9.0`
- Edit `MessengerWebhook.IntegrationTests.csproj` line 4: `net8.0` → `net9.0`
- `dotnet build` cả 2 project pass: **0 errors, 32 warnings pre-existing** (CS1998 async-no-await, CS8625 null-conversion, CS0618 GoogleCredential.FromFile deprecated)

### Strategy package
- **Giữ packages 8.x** (EF Core 8.0.11, AspNetCore.Mvc.Testing 8.0.0, Serilog.AspNetCore 8.0.0, etc.) — net9 runtime backward compat
- Upgrade EF Core 8→9 là breaking change rủi ro cao, **không gom vào upgrade này**. Tạo plan riêng nếu cần (EF 9 có changes: pending migrations detection, JSON columns, etc.)

### Next step
- Build CI cần update runtime image base: `mcr.microsoft.com/dotnet/aspnet:9.0`
- Verify Docker base image trong `docker-compose.yml`

---

## 2. System prompt + personality token count ✅ MEASURED

### Files đo
| File | Chars (Unicode) | Bytes (UTF-8) | Est tokens (Gemini) |
|---|---|---|---|
| `beauty-consultant-system-prompt.txt` | 3,510 | 4,505 | 880–1,400 |
| `personality-traits.txt` | 2,599 | 2,714 | 650–1,040 |
| `sales-closer-system-prompt.txt` | 3,217 | 4,158 | 800–1,290 |
| **TOTAL** | **9,326** | **11,377** | **2,330–3,730** |

### Tỷ lệ chars/token Vietnamese
- ASCII English: 1 token ≈ 4 chars
- Vietnamese có dấu (UTF-8 multi-byte): 1 token ≈ 2.5–3 chars
- Range 2,300–3,700 là conservative upper-lower bound

### Kết luận impact lên Phase 03

**Gemini Context Caching API yêu cầu minimum 32,768 tokens** ([docs.google.com](https://ai.google.dev/gemini-api/docs/caching)).
Total system prompt + personality = **~3,700 tokens (max)** ⇒ **DƯỚI NGƯỠNG 8.6 LẦN**.

→ **Phase 03 Layer 1 (Gemini Context Caching) KHÔNG khả thi** với prompt hiện tại.

**Options**:
- **(A) Drop Layer 1 hoàn toàn** ⇒ Scope Phase 03 = chỉ Layer 2 + Layer 3. Effort giảm xuống ~1.5–2 ngày
- **(B) Inflate prompt lên ≥ 32k**: thêm few-shot examples, expanded policy doc, brand voice corpus — RỦI RO làm prompt loãng, giảm chất lượng output
- **(C) Defer Layer 1 sang khi có RAG context injection** trong prompt — nếu RAG retrieved chunks + history + system >= 32k aggregate. Cần đo lại trong Phase 04

**Khuyến nghị**: **Option A**. Phase 03 update scope.

### Cách đo chính xác hơn (nếu cần)
Dùng Gemini `countTokens` API call thực:
```csharp
var response = await geminiClient.CountTokensAsync(new CountTokensRequest {
    Contents = [...]  // system prompt content
});
// response.TotalTokens là số token thực
```
Code hiện tại chưa expose API này — nếu cần precision tuyệt đối thì thêm endpoint debug `/admin/count-tokens`.

---

## 3. Pinecone .NET SDK v2.0.0 filter syntax ✅ CONFIRMED

### Cú pháp chính xác

`QueryRequest.Filter` nhận **`Metadata`** type (dictionary). Operators dùng nested `Metadata` với key bắt đầu `$`:

```csharp
using Pinecone;

// Equality
Filter = new Metadata { ["tenant_id"] = "tenant-abc" }

// $in (multiple values)
Filter = new Metadata
{
    ["channel_visibility"] = new Metadata
    {
        ["$in"] = new[] { "messenger", "all" }
    }
}

// $eq explicit
Filter = new Metadata
{
    ["policy_version"] = new Metadata { ["$eq"] = "v2026Q2" }
}

// $exists
Filter = new Metadata
{
    ["price_eff_date"] = new Metadata { ["$exists"] = true }
}

// $and compound
Filter = new Metadata
{
    ["$and"] = new[]
    {
        new Metadata { ["tenant_id"] = "tenant-abc" },
        new Metadata { ["content_type"] = new Metadata { ["$in"] = new[] { "product", "faq" } } }
    }
}
```

### Code hiện tại — gap

`PineconeVectorService.cs:289 ConvertToMetadata` chỉ làm **equality match flat**:
```csharp
metadata[kvp.Key] = kvp.Value switch
{
    string s => s,
    double d => d,
    bool b => b,
    _ => kvp.Value.ToString()
};
```

→ Không hỗ trợ operators `$in`/`$eq`/`$exists`.

### Phase 06 — bổ sung helper

Thêm `PineconeFilterBuilder` class:

```csharp
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
}
```

Update Phase 06 Step 4 → thay magic `new { $in = ... }` (syntax sai trong plan hiện tại) bằng `PineconeFilterBuilder.In(...)`.

### Lưu ý "null filter graceful"
Pinecone trả về vector KHÔNG có metadata field → field đó coi như missing.
- `$in: ["messenger","all"]` ⇒ vector cũ KHÔNG có `channel_visibility` sẽ bị **exclude** khỏi result
- Workaround: dùng `$or` để include cả missing case:
  ```csharp
  PineconeFilterBuilder.Or(
      PineconeFilterBuilder.In("channel_visibility", new[]{"messenger","all"}),
      PineconeFilterBuilder.Exists("channel_visibility", exists: false)
  )
  ```

Phase 06 Risk section cần update để mention `$or`+`$exists:false` workaround.

---

## 4. Baseline metrics ❌ CHƯA CÓ → đã thêm Phase 00

### Tình trạng

Tìm `plans/reports/` matching `baseline*` | `260515*` | `260516*` | `260517*` (latency/error/cost):

| Pattern | Found |
|---|---|
| `baseline-*.md` | ❌ Không có (chỉ `test-coverage-baseline-260508-1312-sales-handler.md` — code coverage, không phải runtime metrics) |
| `production-stab*.md` | ❌ Không có |
| Latency p95/p99 snapshot | ❌ Không có |
| Cost/conversation | ❌ Không có |

Phase 02 Production Stabilization plan ghi rõ:
- "Thu thập **7 ngày dữ liệu baseline** sau khi Phase 01 deploy production — **Deferred to post-deploy** (pending 7d production data)"
- "Output: `plans/reports/baseline-260515-*.md` — **Deferred to 2026-05-20+** (requires 7d data)"

Hôm nay là **2026-05-17** → đã có ~4 ngày dữ liệu từ deploy 2026-05-13. Đủ để bắt đầu capture sớm.

### Action đã thực hiện

Tạo **Phase 00: Baseline Metrics Capture** trong plan refactor:
- File: `plans/260517-1156-sales-copilot-research-refactor/phase-00-baseline-capture.md`
- Effort: 1 ngày
- Output: 2 reports (`baseline-260517-prod-snapshot.md` + `baseline-260517-cost-estimate.md`)
- 6 metrics: latency p50/p95/p99, error rate, Gemini API latency, cache hit rate, conversation length histogram, cost/1000 webhook

Update `plan.md`:
- Added Phase 00 row vào phases table
- Total effort: 12-17 → **13-18 ngày**
- Recommended order: `Phase 00 → 01 → (02+03+04 parallel) → 05 → 06; 07 anywhere`
- Pre-flight section ghi nhận: net9 upgrade done, token count measured, Pinecone syntax confirmed

### Why Phase 00 prerequisite Phase 01
- Success Criteria của Phase 02 ("circuit breaker reduce error rate by ≥ 50% in degraded scenarios") cần error rate baseline
- Phase 03 ("cache hit rate ≥ 50%") cần baseline embedding/result cache hit rate hiện tại
- Phase 04 ("reduce token cost ≥ 30%") cần cost/1000 webhook baseline
- Không có baseline = success criteria là số không có ý nghĩa

---

## Tóm tắt files changed

| File | Change |
|---|---|
| `src/MessengerWebhook/MessengerWebhook.csproj` | net8.0 → net9.0 |
| `tests/MessengerWebhook.IntegrationTests/*.csproj` | net8.0 → net9.0 |
| `plans/260517-1156-sales-copilot-research-refactor/plan.md` | Add Phase 00 row, pre-flight notes, recommended order |
| `plans/260517-1156-sales-copilot-research-refactor/phase-00-baseline-capture.md` | NEW phase file |
| `plans/reports/researcher-260517-1224-refactor-plan-questions-answered.md` | NEW (this file) |

## Unresolved questions

1. **EF Core 8→9 upgrade**: ngoài scope hôm nay, có cần plan riêng không? Breaking changes: pending migrations detection mặc định ON, JSON column behavior thay đổi.
2. **Phase 03 scope**: confirm drop Layer 1 (Gemini Context Caching) — chuyển effort sang Layer 2 expand hơn? Có thể add intent-aware semantic cache (cache theo `intent + sub_intent + tenant`).
3. **Phase 06 filter graceful migration**: với 1000 tenant đã có data không có `channel_visibility`, dùng `$or + $exists:false` hay re-index toàn bộ trong maintenance window?
4. **Baseline 4 ngày hay 7 ngày**: Phase 02 plan ghi 7 ngày, hôm nay mới có ~4 ngày data. Wait 3 ngày nữa hay capture 4 ngày trước (acceptable cho ground truth approximate)?
