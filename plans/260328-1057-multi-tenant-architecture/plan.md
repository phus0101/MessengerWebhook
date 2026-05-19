# Multi-Tenant Architecture Implementation Plan

**Date:** 2026-03-28
**Based on:** ADR-001, ADR-002, ADR-004, ADR-006
**Duration:** 12 days (2.4 weeks)
**Cost:** 48M VND development + $10-20/month infrastructure

---

## Overview

Refactor single-tenant MVP to multi-tenant platform using Shared Schema + Row-Level Security pattern.

**Status:** Not Started

| Phase | Description | Duration | Status |
|-------|-------------|----------|--------|
| 1 | Database Schema Migration | 3 days | Not Started |
| 2 | Request Routing & Context | 2 days | Not Started |
| 3 | Caching Layer (Redis) | 2 days | Not Started |
| 4 | Security Hardening | 3 days | Not Started |
| 5 | Testing & Validation | 2 days | Not Started |

---

## Success Criteria

1. **Tenant Isolation:** Zero data leakage between tenants (verified by automated tests)
2. **Performance:** < 5ms overhead for tenant resolution
3. **Cache Hit Rate:** > 95% for tenant/branch routing
4. **Security:** PostgreSQL RLS enabled, sensitive data encrypted
5. **Backward Compatibility:** Múi Xù tenant works without changes

---

## Architecture Decision

**Pattern:** Shared Schema + Row-Level Security (ADR-001 Option A)

**Why:**
- Cost-effective: 1 database cho 10-100 tenants
- EF Core hỗ trợ global query filters
- PostgreSQL RLS battle-tested
- Fast tenant provisioning

**Trade-offs:**
- ⚠️ Data leakage risk → Mitigate với automated tests + RLS
- ⚠️ Noisy neighbor → Monitor query performance per tenant

---

## Phases

### Phase 1: Database Schema Migration
**File:** `phase-01-database-schema-migration.md`
- Add `tenant_id` to all entities
- Create `Tenants` and `Branches` tables
- Migrate existing Múi Xù data
- EF Core migration

### Phase 2: Request Routing & Context
**File:** `phase-02-request-routing-context.md`
- `TenantResolutionMiddleware` (PageId → Branch → Tenant)
- `TenantContext` service
- EF Core global query filters
- Memory cache (L1)

### Phase 3: Caching Layer (Redis)
**File:** `phase-03-caching-layer-redis.md`
- Redis setup (Cloud or self-hosted)
- `TenantAwareCache` wrapper
- L1 (Memory) + L2 (Redis) strategy
- Cache invalidation

### Phase 4: Security Hardening
**File:** `phase-04-security-hardening.md`
- PostgreSQL Row-Level Security
- Encrypt sensitive fields (PageAccessToken, etc.)
- Audit logging
- Tenant isolation tests

### Phase 5: Testing & Validation
**File:** `phase-05-testing-validation.md`
- Unit tests for tenant isolation
- Integration tests for routing
- Load testing (multi-tenant scenarios)
- Security audit

---

## Dependencies

**External:**
- Redis Cloud account (or VPS for self-hosted)
- PostgreSQL 14+ (RLS support)

**Internal:**
- Phase 1 must complete before Phase 2
- Phase 2 must complete before Phase 3
- Phase 4 can run parallel with Phase 3

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Data leakage between tenants | CRITICAL | Automated tests + PostgreSQL RLS |
| Migration breaks existing data | HIGH | Backup before migration + rollback plan |
| Cache invalidation bugs | MEDIUM | TTL-based + monitoring |
| Redis single point of failure | MEDIUM | Redis Sentinel or managed service |

---

## Cost Breakdown

### Development (48M VND)
- Phase 1: 3 days × 4M = 12M
- Phase 2: 2 days × 4M = 8M
- Phase 3: 2 days × 4M = 8M
- Phase 4: 3 days × 4M = 12M
- Phase 5: 2 days × 4M = 8M

### Infrastructure (Monthly)
- Redis Cloud Starter: $10-20/month
- PostgreSQL: No change (existing)
- Monitoring: No change (existing)

### Maintenance (Annual)
- Security updates: 12M/year
- Performance tuning: 8M/year

---

## Timeline

```
Week 1: Phase 1-2 (Schema + Routing)
Week 2: Phase 3-4 (Caching + Security)
Week 3: Phase 5 (Testing)
```

**Target Completion:** 2026-04-18 (3 weeks from now)

---

## Next Steps

1. Review plan với client
2. Backup production database
3. Setup Redis Cloud account
4. Begin Phase 1 implementation
