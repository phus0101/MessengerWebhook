# Phase 06: SLA Definition Complete

**Date**: 2026-05-17 14:30
**Severity**: Medium
**Component**: Production Operations & Observability
**Status**: Completed

## What Happened

Phase 06 of the Production Stabilization Plan completed. All 6 phases + 5 runbook phases are now done. The team now has a formal SLO/SLA framework for the 1000-tenant production deployment.

Two documents created:
- `docs/sla-targets.md`: SLO/SLA definitions with error budgets and measurement methodology
- `docs/runbooks/sla-breach-response.md`: Detection, triage, escalation, and postmortem procedures

## The Brutal Truth

We're publishing targets **without baseline data**. Phase 02 deployed observability 7 days ago, but we need 30 days of production traffic before these numbers stop being architectural guesses. If a target is wrong, we either needlessly freeze deploys or miss real problems. That's the uncomfortable reality.

But doing it this way forces us to validate in 30 days instead of drifting without SLOs until something breaks hard enough to demand them. Sometimes deliberate estimates with a review date beat waiting for perfect data.

## Technical Details

**SLO Targets (Internal):**
- Webhook ack: p99 < 500ms (architectural estimate from handler latency)
- Reply latency: p95 < 5s (estimate from Gemini + Pinecone round-trip)
- Error rate: < 0.5% (historical Facebook reliability + tenant isolation code)
- Uptime: ≥ 99.95% (PostgreSQL + K8s pod redundancy)

**SLA Targets (Customer):**
- Uptime: ≥ 99.9%/month with linear credit table (0.05% below threshold = 5% credit)
- Webhook reception: ≥ 99.95% (stronger than uptime SLA)

**Measurement:** Structured Serilog events to Seq; Seq dashboards + alerts for monitoring; error budget policy triggers deploy freeze at 50% consumption.

**Escalation:** 0→15→30→60 min decision points with templated responses (internal Telegram + customer comms).

## Why This Structure

Three decisions shaped the output:

1. **Admit the gap**: Targets are explicitly marked "initial, validate after 30 days" rather than pretending they're production-proven.

2. **Error budget over uptime percent alone**: A 99.9% SLA sounds good until half the team spends sprint cycles chasing 0.05% violations. Error budget (14.4 minutes/month at 99.95%) ties SLA to deployment velocity—freeze at 50% consumed forces intentional risk tradeoffs.

3. **Response automation via Seq queries**: Don't require an engineer to SSH and write adhoc queries during a breach. Runbook includes copy-paste Seq query templates for latency percentiles, error rate, tenant isolation violations, and webhook drops.

## What We Explicitly Left TBD

- **Cost SLO**: No Gemini or Pinecone usage data yet. Will define after 30 days of billing data.
- **Hallucination rate**: Set to < 0.1% but no validation mechanism (human spot-checks only until we build automated detection).
- **Tenant cross-leak**: Set to zero (kill-switch policy) but detection relies on manual log review.

These are acceptable gaps for now—they don't block breach response.

## Next Steps

**2026-06-17 (30 days):**
- Collect baseline: p50/p95/p99 latencies from Seq, actual error rate, uptime calc from alert logs
- Validate targets: if p99 ack is 200ms, tighten from 500ms; if error rate runs 0.05%, we have 10× buffer to burn
- Adjust SLA credits if targets were wildly off
- Document baseline in `docs/sla-targets.md` with "validated on DATE" note

**Ongoing:**
- Seq alerts feed into team Slack when thresholds breach (set up by Ops in next sprint)
- Postmortem template applied to every error budget depletion (enforce via PR review)
- Cost SLO added once billing stable (60+ days)

This closes out the Production Stabilization Plan entirely. The observability + SLO framework is in place. Now we monitor and learn for 30 days before declaring it production-ready.
