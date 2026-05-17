# Runbook: SLA Breach Response

**Severity**: P1 (SLA breach) / P2 (SLO warning)
**Owner**: On-call dev
**Related**: [SLA Targets](../sla-targets.md)

---

## 1. Detection

A breach is detected when one of the following fires:

| Alert | Condition | Severity |
|-------|-----------|----------|
| High latency warning | Reply p95 > 4.5s for 5 min | P2 |
| High latency breach | Reply p95 > 5s for 5 min | P1 |
| Error rate warning | 5xx > 0.45% for 5 min | P2 |
| Error rate breach | 5xx > 0.5% for 5 min | P1 |
| Message dropped | ≥ 1 drop/min | P1 |
| Uptime breach | Health check failing > 5 min | P1 |

Alerts arrive via Telegram (`TelegramNotifier`) or Seq built-in notification.

---

## 2. First response (0–15 min)

### Triage

```
1. Check Seq dashboard — what error/latency spike looks like
2. Check recent deploys: git log --oneline -5
3. Check Facebook status: https://metastatus.com
4. Check Gemini/Pinecone status pages
5. Determine: platform issue vs. our code vs. external dependency
```

### Seq queries

```sql
-- What's causing errors right now?
@Level in ['Error','Fatal']
| order by @Timestamp desc
| limit 20

-- Which tenants are affected?
@Level in ['Error','Fatal']
| summarize Count = count() by TenantId
| order by Count desc

-- Latency spike — which path?
@MessageTemplate like '%SalesHandlerCompleted%'
| summarize p95 = percentile(ElapsedMs, 95) by ConversationState
| order by p95 desc

-- Message drops
@MessageTemplate like '%MessageDropped%'
| order by @Timestamp desc
```

### Decision tree

```
Deploy < 30 min ago?
  YES → Rollback immediately (see §4)
  NO  → Is it external? (Facebook / Gemini / Pinecone)
          YES → Log incident, monitor, no action needed
          NO  → Investigate root cause (see runbook per alert type)
```

---

## 3. Communication

### Internal (immediate)

Notify team via Telegram:
```
[ALERT] SLA Breach — {metric} exceeded {threshold}
Started: {time}
Affected: {scope — all tenants / specific TenantId}
Status: Investigating
ETA: TBD
```

### Customer-facing (if uptime breach > 15 min)

Send via support channel:
```
We are experiencing elevated response times / errors affecting your Messenger bot.
Our team is actively investigating.
Estimated resolution: TBD
We will update every 30 minutes.
```

**When to notify customers:**
- Uptime breach > 15 min → notify affected tenants
- Error rate > 2% > 15 min → notify all tenants
- Latency breach only → internal only (no customer notification required per SLA)

---

## 4. Rollback

```bash
# Check what's running
git log --oneline -3

# Revert last commit (if deploy-caused)
git revert HEAD --no-edit
dotnet publish -c Release -o /app/publish
# Restart service per deployment method
```

For database migration rollback — see `docs/deployment-guide.md` (if exists) or contact lead.

---

## 5. Escalation

| Time since detection | Action |
|---------------------|--------|
| 0–15 min | On-call dev handles solo |
| 15–30 min | Escalate to lead, consider customer notification |
| 30–60 min | Lead + consider emergency rollback if no fix |
| > 60 min | Customer notification mandatory, postmortem scheduled |

---

## 6. Resolution

When incident is resolved:
1. Confirm metrics back to normal in Seq (5 min stable)
2. Send resolution message to Telegram/customers
3. Record incident in postmortem (see §7)

---

## 7. Postmortem template

**Required for**: any SLA breach (100% error budget consumed) or P0/P1 incident > 30 min.

```markdown
# Postmortem: [Brief title]

**Date**: YYYY-MM-DD
**Duration**: X min (HH:MM – HH:MM UTC+7)
**Severity**: P0 / P1
**SLO impact**: [metric] exceeded [threshold] for [duration]
**Error budget consumed**: X min (Y% of monthly budget)

## Timeline

| Time | Event |
|------|-------|
| HH:MM | Alert fired |
| HH:MM | Investigation started |
| HH:MM | Root cause identified |
| HH:MM | Fix deployed / rollback |
| HH:MM | Metrics stabilized |

## Root cause

[1–2 sentences. What broke and why.]

## Impact

- Tenants affected: [all / list]
- Messages dropped: [count or 0]
- Customer-facing: [yes/no — did customers experience degraded service?]

## What went well

- [e.g., alert fired within 2 min]

## What went wrong

- [e.g., rollback took 15 min due to migration]

## Action items

| Action | Owner | Due |
|--------|-------|-----|
| [Fix] | dev | YYYY-MM-DD |
| [Add test] | dev | YYYY-MM-DD |
| [Update runbook] | dev | YYYY-MM-DD |
```

---

## 8. Error budget tracker

Check monthly budget status in Seq:

```sql
-- Uptime this month (downtime = minutes with error rate > 1%)
-- Approximate: count 5-min windows with error count > threshold

-- Error rate by day
@Level in ['Error','Fatal']
| summarize Count = count() by bin(@Timestamp, 1d)
| order by @Timestamp asc
```

Manual tracking (until automated dashboard):
- Open `docs/sla-targets.md` → Error budget policy
- Record each incident duration in this runbook's incident log below

### Incident log (current month)

| Date | Duration | SLO affected | Budget used |
|------|----------|-------------|-------------|
| — | — | — | — |
