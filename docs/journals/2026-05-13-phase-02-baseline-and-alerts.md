# Phase 02: Baseline Latency & Alerts - Mostly Infrastructure That Already Existed

**Date**: 2026-05-13 14:27
**Severity**: Medium
**Component**: Monitoring, Logging, Alerting
**Status**: Resolved

## What Happened

Phase 02 was supposed to establish baseline latency tracking and alert infrastructure. We committed 83caad4 with test suite passing at 1,096/1,096. But here's what actually occurred: ~80% of this phase's infrastructure already existed from Phase R-05 refactoring. We didn't build a new monitoring system — we discovered we already had one and plugged in missing pieces.

## The Brutal Truth

This is frustrating and also revealing. Three phases in a row (R-05, 01, and now 02), we've discovered that the prior refactoring left us with *working code we didn't know about*. Not broken, not half-finished — genuinely functional. This means either:

1. The R-05 refactoring was more thorough than documented, or
2. Our planning process assumes things are missing without verifying

Both are problems. The first means we're shipping undocumented capability. The second means we're doing redundant work.

What makes Phase 02 sting specifically: we almost shipped a false-positive P1 alert channel that would've been noise in production. The code reviewer caught it, but only *after* we'd written it and tests were passing.

## Technical Details

**What We Actually Changed:**

1. **SalesStateHandlerBase.HandleAsync** — Wrapped handler execution in Stopwatch, logged `SalesHandlerCompleted State={State} ElapsedMs={ElapsedMs}` in finally block (4 lines). This feeds RequestTimingTracker which already existed.

2. **ConversationStateMachine.TransitionToAsync** — Fixed log template PascalCase inconsistency: `StateTransition From={FromState} To={ToState}` (1-line fix for structured log parsing).

3. **WebhookProcessor.ProcessAsync** — Replaced bare `catch` with `catch (Exception ex)`, added typed error logging: `LogError("WebhookError ErrorType={ErrorType}", ex.GetType().Name)` (3-line fix).

4. **SalesContextResolver + SalesReplyOrchestrator** — Fixed pre-existing DI bug discovered during test run: bare `ILogger` constructor injection → `ILogger<T>`. Tester agent found this, not us.

5. **docs/runbooks/** — Created 3 P1 alert runbooks (operational, not code).

**Total net code changes:** ~15 lines across 4 files. The heavy lifting (RequestTimingTracker, TelegramNotifier, AlertDeduplicator, AlertWebhookEndpointExtensions, TelegramOptions, SeqOptions) was already there.

**Build status:** 0 errors. Tests: 1,096/1,096 pass (integration tests now running alongside unit tests).

## What We Tried

- Wrote endpoint-level channel depth alert (800/950 thresholds) alongside existing ChannelMonitoringService check
- Code reviewer flagged: we'd detect channel *pressure* (depth=900) as "MessageDropped" — factually wrong
- Attempted to rename the alert template to fix semantics
- Realized: having two depth checks (endpoint-level + service-level) is redundant and creates false-positive P1s
- **Removed the endpoint check entirely.** ChannelMonitoringService already covers 800/950 thresholds with correct semantics.

## Root Cause Analysis

**Why Phase 02 felt empty:**

The R-05 refactoring (extracting SalesConsultationReplies, ConversationHistoryHelper, splitting Program.cs) wasn't just modularization — it was infrastructure cleanup that left monitoring hooks in place. We documented the *code organization*, not the *capability that resulted*.

**Why we almost shipped false-positive alerts:**

We built P1 alerts based on naming convention ("MessageDropped") without verifying the condition that triggers them. Our code reviewer had to manually read the logic and realize: depth=900 is channel backpressure, not actual message drop. We got lucky the reviewer was thorough. This wouldn't have survived 48 hours in production.

**Why the ILogger<T> bug existed:**

Pre-existing bug in SalesContextResolver/SalesReplyOrchestrator. The DI container accepted bare `ILogger`, but structured logging needs the generic type for source context. This was silently broken for 2+ months. Only failed when tester agent actually ran the integration tests (not the standard unit suite).

## Lessons Learned

1. **Verify Before Planning**: Phase R-05 left us with working infrastructure. We should've audited what existed before planning Phase 02. Instead we assumed we were building from scratch.

2. **Alert Semantics Matter**: Channel depth at 900/950 is *pressure*, not *loss*. We almost named a P1 alert "MessageDropped" when nothing was dropped. A 2-minute review of the condition prevented a production false-positive firestorm.

3. **ILogger<T> Is Non-negotiable**: Bare `ILogger` in DI is a code smell. We caught this in tests, but we should've flagged it during R-05 review. Generic logging loses source context — integration tests were the first to expose this.

4. **Document Infrastructure, Not Just Code**: R-05 left RequestTimingTracker, TelegramNotifier, etc. in place. We need to document *what monitoring capability is available* alongside *what code was refactored*. Right now we document structure, not capability.

## Next Steps

1. **Audit Monitoring Capability** (owner: lead, timeline: before Phase 03) — List all existing alert types, thresholds, and integrations. Update docs/system-architecture.md with monitoring topology. This prevents us from re-discovering features we already built.

2. **Seq Baseline Report** (owner: ops, timeline: 7+ days production data) — Phase 02 assumes we'll have 7 days of production timing data post-deploy. Current baseline is "unknown." Will need Seq query to establish P50/P95/P99 latency percentiles for SalesHandlerCompleted events once we're live.

3. **Fix Remaining DI Type Issues** (owner: current, timeline: before Phase 03) — Search for other bare `ILogger` injections in the codebase. SalesContextResolver/SalesReplyOrchestrator wasn't alone.

## Commit Reference

**83caad4** — Phase 02: Baseline latency tracking + alert runbooks (mostly pre-built, removed false-positive channel depth check, fixed ILogger<T> DI)

---

**Unresolved Questions:**
- Are there other pre-built components from R-05 we haven't discovered yet?
- How many additional bare `ILogger` DI bugs exist in the codebase?
