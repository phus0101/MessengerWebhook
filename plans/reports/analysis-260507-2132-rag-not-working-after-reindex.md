# Báo cáo phân tích vấn đề RAG không hoạt động (Lần 2)

**Ngày:** 2026-05-07 21:32  
**Vấn đề:** Bot vẫn không tìm thấy products sau khi reindex Pinecone

---

## Tóm tắt

Sau khi:
1. ✅ Update TenantId cho 10 products
2. ✅ Xóa embeddings cũ
3. ✅ Reindex vào Pinecone (10 records trong namespace `4dac423d-96ad-44a6-9f33-c78268960c88`)

Bot **VẪN** trả về "chưa tìm thấy dữ liệu sản phẩm phù hợp" cho query "tôi muốn tìm sản phẩm trị thâm nám".

---

## Phát hiện quan trọng

### 1. Database OK
- ✅ 10 products active với TenantId đúng
- ✅ 10 embeddings trong bảng ProductEmbeddings
- ✅ Products có từ khóa "nám": Kem Trị Nám Tàn Nhang, Combo Trị Nám Toàn Diện
- ✅ Database encoding: UTF8

### 2. Pinecone OK
- ✅ Namespace `4dac423d-96ad-44a6-9f33-c78268960c88`: 10 records
- ✅ Total records: 20 (10 ở namespace cũ + 10 ở namespace mới)

### 3. Conversation Session
- ✅ Session có TenantId đúng: `4dac423d-96ad-44a6-9f33-c78268960c88`
- ❌ **Session KHÔNG có messages** (msg_count = 0)
- ⚠️ LastActivityAt: 14:29:20 (7 tiếng trước test lúc 21:30)

---

## Nguyên nhân có thể

### A. TenantContext không được resolve trong webhook flow

**Code:** `RAGService.cs:54-58`
```csharp
if (!_tenantContext.TenantId.HasValue)
{
    _logger.LogWarning("RAG retrieval skipped because tenant context is not resolved");
    return CreateEmptyContext(...);
}
```

**Vấn đề:** Nếu TenantContext không được resolve, RAG sẽ bị skip và trả về empty context.

**Cách verify:** Kiểm tra logs có warning "RAG retrieval skipped" không.

### B. Messages không được persist vào database

**Phát hiện:** Session có `LastActivityAt` được update nhưng `message_count = 0`.

**Vấn đề:** Conversation flow có thể bị lỗi ở bước persist messages, dẫn đến:
- Bot xử lý message
- RAG được gọi
- Nhưng messages không được lưu vào DB

**Cách verify:** Kiểm tra logs có error khi save messages không.

### C. RAG disabled hoặc RagService = null

**Code:** `SalesStateHandlerBase.cs:1232-1235`
```csharp
if (!RagOptions.Enabled || RagService == null)
{
    return new RAGContext(string.Empty, ...);
}
```

**Config:** `appsettings.json`
```json
"RAG": {
  "Enabled": true,
  "TopK": 5,
  "FallbackStrategy": "full-context",
  "TimeoutMs": 5000
}
```

**Cách verify:** Check config và DI registration.

### D. Redis cache trả về kết quả cũ

**Config:** `appsettings.json`
```json
"Redis": {
  "ConnectionString": "localhost:6379",
  "InstanceName": "messenger-rag:",
  "Enabled": true
}
```

**Vấn đề:** Cache có thể chứa kết quả cũ (empty results) từ trước khi reindex.

**Cách verify:** Flush Redis cache hoặc disable Redis temporarily.

### E. Pinecone search không match

**Vấn đề:** Semantic embedding của query "tôi muốn tìm sản phẩm trị thâm nám" không match với embeddings của products.

**Nguyên nhân có thể:**
- Embedding model khác nhau (training vs inference)
- Query embedding không được tạo đúng
- Cosine similarity threshold quá cao

**Cách verify:** Test trực tiếp Pinecone search với query embedding.

---

## Khuyến nghị tiếp theo

### 1. Enable detailed logging (CRITICAL)

**Vấn đề:** Không có logs mới để debug (log file cũ nhất từ 19/4).

**Action:**
```bash
# Kiểm tra app có chạy không
netstat -ano | findstr :5030

# Nếu app đang chạy, restart để tạo log file mới
# Nếu không chạy, start app với logging enabled
dotnet run --project src/MessengerWebhook
```

### 2. Flush Redis cache

**Action:**
```bash
docker exec -it messenger-redis redis-cli FLUSHALL
```

### 3. Test RAG endpoint trực tiếp

**Action:**
```bash
# Login admin
POST /admin/login
{
  "email": "admin@example.com",
  "password": "xxx"
}

# Test RAG
POST /api/admin/test-rag
{
  "query": "sản phẩm trị thâm nám",
  "topK": 5
}
```

### 4. Verify TenantContext resolution

**Check:** `TenantResolutionMiddleware.cs:42-65`

**Verify:**
- FacebookPageId được extract từ webhook payload
- FacebookPageConfig được lookup đúng
- TenantId được initialize vào TenantContext

### 5. Test conversation flow end-to-end

**Action:**
- Gửi message mới qua Messenger
- Monitor logs real-time
- Verify messages được persist vào DB
- Verify RAG được gọi với TenantId đúng

---

## Kết luận tạm thời

**Setup infrastructure:** ✅ OK (Database, Pinecone, Products, Embeddings)

**Runtime flow:** ❌ CHƯA VERIFY (TenantContext, RAG execution, Message persistence)

**Next critical step:** Enable logging và test conversation flow end-to-end để xác định chính xác vấn đề nằm ở đâu trong runtime flow.

---

## Unresolved Questions

1. Tại sao session có `LastActivityAt` được update nhưng không có messages?
2. TenantContext có được resolve đúng trong webhook flow không?
3. RAG có được gọi không? Nếu có, tại sao không tìm thấy products?
4. Redis cache có chứa kết quả cũ không?
5. Logs của app đang được ghi ở đâu? (log file cũ nhất từ 19/4)
