# Tóm tắt phân tích vấn đề RAG

**Ngày:** 2026-05-07 21:37  
**Vấn đề:** Bot không tìm thấy products sau khi reindex Pinecone

---

## Phát hiện chính

### Infrastructure: ✅ OK
- Database: 10 products với TenantId `4dac423d-96ad-44a6-9f33-c78268960c88`
- Pinecone: 10 records trong namespace đúng
- Embeddings: 10 embeddings đã được tạo
- Config: RAG.Enabled = true

### Runtime: ❌ KHÔNG THỂ VERIFY
- **Thư mục logs không tồn tại** → Đã tạo mới
- **Không có log files** → App chưa ghi logs hoặc chưa chạy
- **4 process dotnet đang chạy** → Không rõ process nào là MessengerWebhook
- **Session có TenantId đúng nhưng không có messages** (msg_count = 0)

---

## Nguyên nhân có thể

### 1. App không chạy hoặc chạy sai port
- Port 5030 có thể không được listen
- App có thể crash khi start
- App có thể chạy nhưng không ghi logs

### 2. TenantContext không được resolve
```csharp
// RAGService.cs:54-58
if (!_tenantContext.TenantId.HasValue)
{
    _logger.LogWarning("RAG retrieval skipped because tenant context is not resolved");
    return CreateEmptyContext(...);
}
```

### 3. Messages không được persist
- Conversation flow có thể bị lỗi khi save messages
- Session được update nhưng messages không được lưu

### 4. Redis cache trả về kết quả cũ
- Cache có thể chứa empty results từ trước khi reindex

---

## Action items (Theo thứ tự ưu tiên)

### 1. Verify app đang chạy
```powershell
# Kiểm tra port 5030
Get-NetTCPConnection -LocalPort 5030 -ErrorAction SilentlyContinue

# Nếu không có, start app
dotnet run --project src/MessengerWebhook
```

### 2. Monitor logs real-time
```powershell
# Terminal 1: Start app
dotnet run --project src/MessengerWebhook

# Terminal 2: Monitor logs
Get-Content logs\app-*.log -Wait -Tail 50
```

### 3. Flush Redis cache
```powershell
docker exec -it messenger-redis redis-cli FLUSHALL
```

### 4. Test conversation mới
- Gửi message qua Messenger
- Monitor logs để xem:
  - TenantContext có được resolve không?
  - RAG có được gọi không?
  - Messages có được persist không?

### 5. Test RAG endpoint trực tiếp
```bash
POST /api/admin/test-rag
{
  "query": "sản phẩm trị thâm nám",
  "topK": 5
}
```

---

## Kết luận

**Vấn đề chính:** Không thể debug vì không có logs.

**Bước tiếp theo quan trọng nhất:** Start app với logging enabled và monitor logs real-time khi test conversation.

Chỉ khi có logs mới có thể xác định chính xác:
- TenantContext có được resolve không?
- RAG có được gọi không?
- Tại sao messages không được persist?
- Pinecone search có trả về kết quả không?
