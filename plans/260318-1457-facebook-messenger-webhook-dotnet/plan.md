# Kế hoạch: Facebook Messenger Webhook System (.NET)

**Mục tiêu:** Xây dựng hệ thống webhook Facebook Messenger với ASP.NET Core 8 Minimal API, xử lý bất đồng bộ, bảo mật cao.

**Chỉ tiêu hiệu suất:**
- Webhook response: < 100ms (P95)
- Processing latency: < 5s (P95)
- Throughput: 100+ webhooks/giây

## Research Reports

- [Facebook Messenger API](../reports/researcher-260318-1431-facebook-messenger-api.md)
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md)

## Kiến trúc

```
Minimal API Endpoint → Signature Validation → Channel Queue → BackgroundService → Facebook Graph API
```

**Tech Stack:**
- ASP.NET Core 8 Minimal API
- System.Threading.Channels (async processing)
- Polly v8 (retry policies)
- HMAC-SHA256 signature validation

## Phases

| Phase | Status | Description |
|-------|--------|-------------|
| [Phase 1](phase-01-project-setup.md) | Completed | Project setup, dependencies, configuration |
| [Phase 2](phase-02-webhook-verification.md) | Completed | GET /webhook verification endpoint |
| [Phase 3](phase-03-webhook-events.md) | Pending | POST /webhook event endpoint |
| [Phase 4](phase-04-signature-validation.md) | Pending | HMAC-SHA256 signature validation |
| [Phase 5](phase-05-async-processing.md) | Pending | Channel-based background processing |
| [Phase 6](phase-06-graph-api.md) | Pending | Facebook Graph API integration |
| [Phase 7](phase-07-logging-monitoring.md) | Pending | Structured logging & monitoring |
| [Phase 8](phase-08-testing.md) | Pending | Unit & integration tests |
| [Phase 9](phase-09-deployment.md) | Pending | Docker & deployment config |

## Dependencies

- Phase 2-3 → Phase 4 (signature validation)
- Phase 4 → Phase 5 (async processing)
- Phase 5 → Phase 6 (Graph API)
- Phase 1-6 → Phase 7 (logging)
- Phase 1-7 → Phase 8 (testing)
- Phase 1-8 → Phase 9 (deployment)

## Unresolved Questions

1. Expected webhook volume (messages/hour)?
2. Multi-page support requirement?
3. Message persistence requirement?
4. Deployment target (Azure, Docker, IIS)?
