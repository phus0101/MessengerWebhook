# Runbook: Channel Critical Capacity (P1)

**Alert**: `@Level = 'Error' and @MessageTemplate contains 'Channel at critical capacity'` — count > 0 / 1 phút  
**Severity**: P1 — ngay lập tức, events có thể đang bị drop  
**Owner**: On-call dev

**Source**: `ChannelMonitoringService` — logs every 10s:
- Warning (80%): `"Channel approaching capacity: {Count}/1000"`
- Critical/Error (95%): `"Channel at critical capacity: {Count}/1000 - events may be dropped!"`

## Diagnosis (2 phút)

1. **Xem channel depth trend**:
   ```
   @MessageTemplate contains 'Channel' and @MessageTemplate contains 'capacity'
   | summarize MaxCount=max(Count), Level=max(@Level) by bin(@Timestamp, 1m)
   | order by @Timestamp desc
   ```
2. **Volume spike theo tenant** (tìm tenant gây tải cao):
   ```
   @MessageTemplate startswith 'WebhookCompleted'
   | summarize Count=count() by TenantId, bin(@Timestamp, 1m)
   | order by Count desc | limit 10
   ```
3. **Background consumer còn sống không**:
   - Check health endpoint: `GET /health` → `channel_queue`
   - Tìm log của `WebhookCompleted` gần nhất — nếu không có → consumer stuck

## Nguyên nhân thường gặp

| Nguyên nhân | Dấu hiệu | Fix |
|-------------|----------|-----|
| Background processor chết | Channel depth tăng đều, không giảm | Restart app ngay |
| Traffic spike (viral post) | Depth tăng đột biến 1 tenant | Rate-limit tenant, scale ngang nếu có |
| Xử lý chậm do Gemini lag | Depth tăng, AICallCompleted chậm | Tắt tạm `RAG:Enabled=false`, restart |
| DB deadlock | Processor stuck ở DB op | Kill PG idle transactions, restart |

## Fix ngay

```bash
# Restart service (xử lý nhanh nhất)
# Channel dùng DropOldest mode — restart làm mất tin nhắn trong queue
# Chấp nhận loss, ưu tiên recover service

# Sau restart, verify processor hoạt động:
# Seq query: @MessageTemplate startswith 'WebhookCompleted' — phải có event mới
```

## Kiến trúc channel

- Capacity: 1000, `BoundedChannelFullMode.DropOldest`
- ChannelMonitoringService check interval: 10 giây
- Warning ≥ 800 (80%): cảnh báo sớm
- Critical ≥ 950 (95%): gần drop thật — hành động ngay

## Escalation

Processor không recover sau restart → escalate. Traffic spike: cân nhắc scale hoặc tắt bot tenant ít quan trọng.
