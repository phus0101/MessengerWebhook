# Phase 01: Structured Logging + Seq + Correlation ID + PII Sweep

**Date**: 2026-05-13 10:23
**Severity**: Medium
**Component**: Observability, Logging, PII Redaction
**Status**: Resolved

## What Happened

Phase 01 declared "observability infrastructure" but found most infra already baked into Phase R-05 work: CorrelationIdMiddleware, LogContextEnricher, PiiRedactor, SeqOptions, ObservabilityRegistration, and docker-compose.observability.yml were production-ready. Actual work scoped to three tasks: PII audit/sweep, OTLP tracing plumbing, and template updates. Delivered 218 insertions, 93 deletions across 29 files. Build clean, all 849 tests pass.

## The Brutal Truth

Phase felt like we were backfilling after Phase R-05 already shipped 80% of the solution. But that's actually *good* — it means refactoring already de-risked the observability layer. The frustration was discovering a much larger PII surface than originally scoped (63 raw PSID log calls vs. the ~12 we anticipated). User chose to fix all 63 in one pass rather than half-measure, which meant audit fatigue but eliminated future rework. The real kick in the teeth: we found hardcoded credentials in appsettings.json (Seq ApiKey, Telegram BotToken, InternalApiKey) that should have been env variables from day one.

## Technical Details

**PII Sweep:** 63 raw Facebook PSID log calls removed across 22 files. SalesStateHandlerBase carried 15 (highest concentration). Pattern was consistent: `_logger.LogInformation("User {psid}...")` → removed entirely. PsidHash (SHA256, 12-char hex) already flows via WebhookProcessor → LogContext, so PSID resolution isn't lost, just hashed for security. File sink output template updated to include `{PsidHash}` so file logs carry the correlation.

**OTLP Tracing:** Added 4 OpenTelemetry NuGet packages (Activities, Exporting, Exporting.Otlp, SeqExporter). Conditional export block in ObservabilityRegistration: only activates if OtlpEndpoint is non-empty in config. Uses Uri.TryCreate validation to prevent malformed URLs from crashing startup. Seq 2024.1+ ingests OTLP natively — zero extra infrastructure overhead.

**Error Example:** Before sweep, logs showed:
```
User 1234567890 sent message. State: Qualified. PSIDHash: abc123def456
```
After:
```
Sent message. State: Qualified. PSIDHash: abc123def456
```

## What We Tried

1. **Partial audit first** — Started with originally scoped files only, but realized we were leaving ticking bombs in ConversationStateMachine, CSATSurveyService, and SessionManager. Expanded scope.
2. **Keep raw PSID as debug log level** — Rejected. Debug logs leak to Seq in dev/staging anyway. Cleaner to remove entirely and trust LogContext flow.
3. **OTLP always-on** — Disabled by default (empty config string). Early versions had conditional endpoint validation but it leaked into startup logs. Uri.TryCreate silently handles null/empty now.

## Root Cause Analysis

**Pre-built Infra:** Phase R-05 refactoring already extracted CorrelationIdMiddleware, LogContextEnricher, and PiiRedactor into reusable services. Phase 01 inherited a working system and had to backfill the *actual usage* (the sweep). This is good architecture but created false estimate — looked like full phase of work, was actually finish line.

**Credential Exposure:** Hardcoded values in appsettings.json (Seq ApiKey, InternalApiKey, Telegram BotToken) exist because they were development conveniences that weren't rotated out. Source control visibility means these dev credentials should be treated as compromised *if the repo goes public or is cloned on shared machines.*

## Lessons Learned

1. **Refactoring de-risks incrementally.** Phase R-05 built the pipes; Phase 01 plugged the leaks. Don't fear carrying forward prior phase's infrastructure — it's a feature, not overhead.

2. **PII audit scope creep is unavoidable.** Estimate for "known log sites," but run a codewide grep for the pattern once committed. We found 5x more than anticipated. Plan for it.

3. **Credentials in appsettings.json are a ticking bomb.** Even if values are "just for dev," they shouldn't live in checked-in config. Use GitHub Secrets or local .env for dev, never ship credentials.

4. **Conditional OTLP on empty config is the right call.** Zero overhead when disabled, zero log spam, zero startup cost. Beats feature-flag complexity.

## Next Steps

**CRITICAL-1 (Credentials):** Rotate Seq ApiKey, Telegram BotToken, InternalApiKey immediately if repo has been cloned to shared machines or remote visibility. If repo is strictly local, update appsettings.json to strip values and document expected environment variables in .env.example. Ownership: DevOps/Security. Timeline: Before any remote push.

**Follow-up Testing:** Run observability tests in docker-compose.observability.yml stack to verify Seq ingestion works end-to-end with hashed PSID correlation. Ownership: QA. Timeline: Phase 02 acceptance.

**Commit:** 5bf0c20 — Phase 01 complete.

---

**Unresolved Questions:**
- Have credentials in appsettings.json been committed to any upstream remotes? (Check if repo is local-only or has remote origin.)
- Should OTLP export default to stderr trace exporter (verbose logging) for local dev, or keep silent until explicitly enabled? (Currently silent — might hide issues early.)
