# Runbook: Webhook 5xx Burst (P1)

**Alert**: `@Level in ['Error','Fatal'] and @MessageTemplate contains 'Webhook'` — count > 50 / 5 min  
**Severity**: P1 — wake up  
**Owner**: On-call dev

## Diagnosis (5 phút)

1. **Seq query — lỗi gần nhất**:
   ```
   @Level in ['Error','Fatal'] and @MessageTemplate contains 'Webhook'
   | order by @Timestamp desc
   | limit 20
   ```
2. **Xác định lỗi tập trung ở tenant nào**:
   ```
   @Level in ['Error','Fatal'] and @MessageTemplate contains 'Webhook'
   | summarize Count = count() by TenantId
   | order by Count desc
   ```
3. **Xem stack trace** của error đầu tiên trong Seq UI

## Nguyên nhân thường gặp

| Nguyên nhân | Dấu hiệu | Fix |
|-------------|----------|-----|
| Gemini quota exhausted | `ErrorType=HttpRequestException`, `429` trong message | Chờ quota reset (hàng phút), tắt AI features tạm |
| DB connection pool hết | `ErrorType=NpgsqlException`, timeout | Restart app, check PG connections |
| Facebook token hết hạn | `ErrorType=FacebookApiException`, `190` | Refresh token qua Meta Business |
| Pinecone down | `ErrorType=HttpRequestException`, Pinecone host | RAG fallback đã bật, chờ recover |
| Bug mới deploy | Lỗi tập trung sau deploy | Rollback ngay |

## Rollback

```bash
# Rollback về commit trước
git revert HEAD --no-edit
dotnet publish && restart service
```

## Escalation

Nếu lỗi > 15 phút chưa resolve → escalate lên lead + ping group Telegram thủ công.
