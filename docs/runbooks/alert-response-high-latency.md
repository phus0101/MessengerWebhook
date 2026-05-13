# Runbook: P95 Latency Cao (P1)

**Alert**: `WebhookCompleted` p95 > 8000ms / 5 phút  
**Severity**: P1 — wake up  
**Owner**: On-call dev

## Diagnosis (5 phút)

1. **Latency breakdown theo path**:
   ```
   @MessageTemplate startswith 'WebhookCompleted'
   | summarize p50=percentile(ElapsedMs,50), p95=percentile(ElapsedMs,95) by Path
   | order by p95 desc
   ```
2. **Latency theo tenant** (tìm outlier):
   ```
   @MessageTemplate startswith 'WebhookCompleted'
   | summarize p95=percentile(ElapsedMs,95), Count=count() by TenantId
   | where Count > 10
   | order by p95 desc | limit 10
   ```
3. **AI call timing**:
   ```
   @MessageTemplate startswith 'AICallCompleted'
   | summarize p95=percentile(ElapsedMs,95) by Service
   ```
4. **Sales handler timing**:
   ```
   @MessageTemplate startswith 'SalesHandlerCompleted'
   | summarize p95=percentile(ElapsedMs,95) by State
   | order by p95 desc
   ```

## Nguyên nhân thường gặp

| Nguyên nhân | Dấu hiệu | Fix |
|-------------|----------|-----|
| Gemini latency tăng | AICallCompleted p95 > 5s | Retry với model nhẹ hơn, check quota |
| Pinecone cold start | RAG path chậm, embedding ok | Warm-up query, check index stats |
| DB slow query | `SalesHandlerCompleted` chậm, AI ok | Check PG slow query log, EXPLAIN ANALYZE |
| 1 tenant outlier kéo p95 | p95 theo tenant cao 1 tenant | Isolate tenant: tắt AI features, escalate |
| Redis miss cao | Cache miss → Pinecone liên tục | Check Redis connection, flush key prefix |

## Nhanh nhất

```bash
# Check DB connections và slow queries
psql -h localhost -p 5433 -U postgres -d messenger_bot \
  -c "SELECT pid, query, state, wait_event_type FROM pg_stat_activity WHERE state != 'idle' LIMIT 20;"
```

## Escalation

Latency > 10s p95 trong 15 phút → escalate. 1 tenant outlier → tắt bot tenant đó tạm, không rollback toàn hệ thống.
