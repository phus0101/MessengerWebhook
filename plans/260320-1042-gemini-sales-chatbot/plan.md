# Gemini Pro 3.1 AI Sales Chatbot Implementation Plan

**Created**: 2026-03-20
**Updated**: 2026-03-22
**Status**: In Progress
**Priority**: High
**Estimated Duration**: 12 weeks MVP + 10 months scale
**Overall Progress**: 50% (4 of 8 phases complete)

---

## Overview

Transform existing ASP.NET Core 8 webhook (basic echo bot) into AI-powered cosmetics store beauty consultant using Gemini Pro 3.1. System will guide customers through skin analysis, ingredient-based product matching, handle natural conversations in Vietnamese, and create draft orders via Facebook Messenger.

---

## Current System

- **Framework**: ASP.NET Core 8 (targeting .NET 9)
- **Architecture**: Channel-based async processing (1000 event capacity)
- **Features**: Message/postback handling, idempotency (48h TTL), signature validation, health checks
- **Services**: WebhookProcessor, MessengerService (Graph API v21.0)
- **Limitations**: Echo bot only, no state management, no AI, no product catalog

---

## Target Architecture

- **AI Engine**: Gemini Pro 3.1 (skin consultation) + Flash-Lite (simple queries)
- **RAG Layer**: pgvector for semantic product search based on ingredients/skin-type
- **State Management**: Database-backed state machine with conversation history
- **Product Catalog**: Cosmetics with ingredients, skin types, compatibility rules
- **Order System**: Draft order creation with skin profile collection
- **Cost Target**: <$0.15 per conversation (Pro 50% / Flash-Lite 50%), ~$75-100/month for 1000 conversations/day

---

## Implementation Phases

### ✅ Phase 1: Database Setup
**File**: [phase-01-database-setup.md](./phase-01-database-setup.md)
**Duration**: 1.5 weeks
**Status**: Completed
**Dependencies**: None

Design and implement database schema for cosmetics products (ingredients, skin types), variants, orders, sessions, skin profiles, and conversation state.

---

### ✅ Phase 2: Gemini Integration
**File**: [phase-02-gemini-integration.md](./phase-02-gemini-integration.md)
**Duration**: 1 week
**Status**: Completed
**Dependencies**: Phase 1

Integrate Google Gen AI .NET SDK, implement retry logic, error handling, file-based system prompt loading for beauty consultant.

---

### 🆕 Phase 2.5: RAG Layer
**File**: [phase-02.5-rag-layer.md](./phase-02.5-rag-layer.md)
**Duration**: 2 weeks
**Status**: Pending
**Dependencies**: Phase 1, Phase 2

Implement pgvector extension, embedding generation (text-embedding-004), semantic product search based on ingredients and skin types, ingredient compatibility matching.

---

### ✅ Phase 3: State Machine
**File**: [phase-03-state-machine.md](./phase-03-state-machine.md)
**Duration**: 1.5 weeks
**Status**: ✅ Completed (2026-03-22)
**Dependencies**: Phase 1, Phase 2, Phase 2.5
**Progress**: 100% (139 unit tests passing, code review 8.5/10)

Build conversation state machine with 17 states, 11 handlers, session management, conversation history (30-day retention). DI registration bug fixed, language detection added, WebhookProcessor integrated.

---

### ✅ Phase 4: Product Catalog
**File**: [phase-04-product-catalog.md](./phase-04-product-catalog.md)
**Duration**: 2 weeks
**Status**: Pending
**Dependencies**: Phase 1, Phase 2.5

Implement semantic product search (RAG), ingredient-based filtering, skin-type compatibility matching, variant selection (volume/texture), and Messenger templates for cosmetics.

---

### ✅ Phase 5: Conversation Flows
**File**: [phase-05-conversation-flows.md](./phase-05-conversation-flows.md)
**Duration**: 2 weeks
**Status**: Pending
**Dependencies**: Phase 2, Phase 3, Phase 4

Design beauty consultant prompts, implement skin profile extraction, build conversation flows for skin consultation, ingredient compatibility checking, and product recommendations.

---

### ✅ Phase 6: Order Workflow
**File**: [phase-06-order-workflow.md](./phase-06-order-workflow.md)
**Duration**: 1.5 weeks
**Status**: Pending
**Dependencies**: Phase 3, Phase 4, Phase 5

Implement cart management, address collection, order creation, and payment integration (Stripe/PayPal/COD).

---

### ✅ Phase 7: Testing & Optimization
**File**: [phase-07-testing-optimization.md](./phase-07-testing-optimization.md)
**Duration**: 1.5 weeks
**Status**: Pending
**Dependencies**: All previous phases

Load testing (RAG queries), cost optimization, monitoring setup, security audit, and production hardening.

---

### 🆕 Phase 8: Multi-Tenant Architecture
**File**: [phase-08-multi-tenant-architecture.md](./phase-08-multi-tenant-architecture.md)
**Duration**: 10 months (4 sub-phases)
**Status**: Planned
**Dependencies**: Phase 1-7 complete (MVP)

Scale architecture to support multiple stores (tenants), multiple branches per store, and multiple product categories. See [Architecture Decision Records](../../docs/architecture-decision-records.md) for detailed design.

**Sub-phases**:
- **Phase 8.1**: Multi-Branch Foundation (2 months) - Branch entity, PageId routing, branch-specific inventory
- **Phase 8.2**: Multi-Tenant Core (3 months) - Tenant isolation, row-level security, tenant-aware caching
- **Phase 8.3**: Multi-Category Support (3 months) - Polymorphic schema, category plugins, per-category RAG
- **Phase 8.4**: Scaling Infrastructure (2 months) - Redis, Kubernetes, auto-scaling

---

## Key Technical Decisions

### AI Model Strategy (Hybrid Approach)
- **Gemini 3.1 Flash-Lite**: Simple queries (availability, greetings, order status) - 50% of traffic
- **Gemini 3.1 Pro**: Complex consultation (skin analysis, ingredient matching, compatibility checking) - 50% of traffic
- **Rationale**: Cosmetics consultation requires more complex reasoning than clothing, hence higher Pro usage

### State Management
- **Storage**: SQL Server with EF Core
- **Caching**: IMemoryCache for single instance (Redis optional for scale)
- **Timeout**: 15min inactivity, 60min absolute, 30min cart expiration

### Conversation History
- **Storage**: Database-backed with 30-day retention (legal compliance)
- **Context Window**: Last 10 turns for Gemini API
- **Token Optimization**: Reference-based context (product IDs, skin profile summary)
- **Cleanup**: Automated job deletes messages older than 30 days

---

## Success Metrics

- **Response Time**: <1s first response (streaming), <500ms subsequent turns
- **Availability**: 99.5% with fallback strategies
- **Cost**: <$0.15 per conversation (10 turns avg, Pro 50%)
- **Conversion**: Track cart-to-order conversion rate
- **Skin Profile Accuracy**: >90% correct skin type extraction
- **Ingredient Match Quality**: >85% relevant product recommendations
- **User Satisfaction**: Track conversation completion rate

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Gemini API rate limits | High | Implement exponential backoff, circuit breaker, fallback responses |
| High API costs | Medium | Hybrid model approach, context summarization, spend caps |
| Session state loss | Medium | Database persistence, Redis backup, session recovery |
| Vietnamese language quality | Medium | Test extensively, fine-tune prompts, human handoff option |
| Payment integration complexity | Low | Start with COD, add gateway later |

---

## Research Reports

- **Gemini API**: [researcher-260320-1042-gemini-api.md](../reports/researcher-260320-1042-gemini-api.md)
- **Order Management**: [researcher-260320-1042-order-management.md](../reports/researcher-260320-1042-order-management.md)

---

## Next Steps

1. Review and approve this plan
2. Set up development database (SQL Server/PostgreSQL)
3. Obtain Gemini API key from Google AI Studio
4. Begin Phase 1: Database Setup
5. Create product catalog seed data (sample clothing items)

---

## Unresolved Questions

1. **Payment Gateway**: Stripe, PayPal, or local Vietnamese provider (SePay, VNPay)?
2. **Database**: PostgreSQL confirmed for pgvector support
3. **Ingredient Database**: Source for ingredient compatibility rules? Manual entry or API?
4. **Human Handoff**: Dermatologist consultation integration?
5. **Multi-language**: Vietnamese only or add English support?
6. **Shipping**: Flat rate or location-based calculation?
7. **Analytics Platform**: Application Insights, custom, or third-party?
8. **Skin Profile Validation**: How to verify AI-extracted skin profiles?
9. **Product Images**: Storage strategy (local, S3, CDN)?
10. **Multi-Tenant Timeline**: Start Phase 8 immediately after MVP or wait for user validation?
