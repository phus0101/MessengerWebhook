# Phase 03 — Validate Command and Docs

## Context Links

- `docs/project-changelog.md`
- `docs/codebase-summary.md`
- `docs/code-standards.md`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/LiveAiRagTranscriptIntegrationTests.cs`

## Overview

Priority: medium  
Status: completed  
Compile the implementation, verify default tests remain safe, and document the opt-in live command.

## Key Insights

- Live tests must not run in normal CI/test flow.
- Project docs require changelog updates after significant testing or bug-fix support work.
- Compile/build validation is mandatory after code changes.

## Requirements

### Functional

- Run build after implementing test code.
- Run default relevant integration/unit tests without env gate to confirm no live API calls happen.
- Run live test only with explicit env var when local API credentials are present.
- Update docs minimally with the new live validation command.

### Non-Functional

- Without `RUN_LIVE_AI_RAG_TESTS=true`, live test returns early and performs no external calls.
- With `RUN_LIVE_AI_RAG_TESTS=true`, missing config/API keys or missing indexed MN RAG data must fail clearly.
- Do not weaken existing deterministic regression tests.
- Keep documentation concise.

## Architecture

```text
implementation
  -> dotnet build
  -> dotnet test relevant default scope without RUN_LIVE_AI_RAG_TESTS
  -> assert default run returns early with no external calls
  -> set RUN_LIVE_AI_RAG_TESTS=true
  -> dotnet test live filter
  -> fail clearly on missing config/API key/indexed MN data
  -> docs/changelog update
```

## Related Code Files

### Modify/Create

- `docs/project-changelog.md` if implementation adds live validation test.
- Optional existing docs file if there is a dedicated testing guide.

### Read Only

- `docs/code-standards.md`
- `docs/codebase-summary.md`

## Implementation Steps

1. Run compile:
   ```powershell
   dotnet build
   ```
2. Run default relevant tests without env gate:
   ```powershell
   dotnet test tests/MessengerWebhook.IntegrationTests --filter "FullyQualifiedName~LiveAiRagTranscriptIntegrationTests"
   ```
   Expected: skipped/inert, no live API call.
3. Run live test locally only when credentials and live data are ready:
   ```powershell
   $env:RUN_LIVE_AI_RAG_TESTS="true"
   dotnet test tests/MessengerWebhook.IntegrationTests --filter "FullyQualifiedName~LiveAiRagTranscriptIntegrationTests"
   ```
4. If live test fails due config, API key, or missing indexed MN/product RAG data, report exact missing prerequisite and do not hide failure.
5. Confirm the test does not index/upsert Pinecone data.
6. Update changelog with a concise entry for live transcript validation support.
7. Delegate final test validation to `tester` agent.
8. Delegate code review to `code-reviewer` agent.
9. Delegate docs update/review to `docs-manager` if docs changed beyond changelog.

## Todo List

- [x] Run `dotnet build`.
- [x] Confirm default test run is inert without env var.
- [x] Run opt-in live command when config is available.
- [x] Update changelog/docs.
- [x] Run tester agent.
- [x] Run code-reviewer agent.

## Success Criteria

- Build passes.
- Default tests do not consume external AI/RAG APIs.
- Opt-in command is documented and works with valid local credentials and already-indexed MN/product RAG data.
- Opt-in command fails clearly if required live config/data is absent.
- Changelog records live transcript validation support.

## Risk Assessment

- Live command may be flaky due external services. Mitigation: only use as pre-restart diagnostic, not required in normal CI.
- API quota/cost. Mitigation: explicit env gate and narrow filter command.
- Documentation drift. Mitigation: keep command in changelog or existing testing docs only.

## Security Considerations

- Do not include secrets in docs or test output.
- Do not push `.env` or local config.
- Use env var names only in failure messages.

## Validation Evidence

- `dotnet build`: passed with 0 warnings/errors.
- `dotnet test tests/MessengerWebhook.UnitTests`: passed 659/659.
- `dotnet test tests/MessengerWebhook.IntegrationTests`: final run passed 235/235, skipped 7 expected live/API-gated tests.
- `dotnet test tests/MessengerWebhook.IntegrationTests --filter "FullyQualifiedName~LiveAiRagTranscriptIntegrationTests"`: env-off run passed inertly.
- Env-on live AI/RAG transcript run passed with local credentials and indexed MN data.
- Tester agent independently validated build, full unit, targeted regression, and full integration suites.
- Code reviewer re-review found no remaining critical/high tenant isolation issues.

## Next Steps

Plan completed. Ask before committing or pushing changes.
