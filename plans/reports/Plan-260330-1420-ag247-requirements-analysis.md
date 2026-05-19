# AG247 Requirements Analysis & Jira Task List

**Date**: 2026-03-30 14:20
**Project**: Multi-Tenant Messenger Chatbot Platform
**Client**: Mui Xu Cosmetics (AG247)

## Executive Summary

Analysis of customer requirements vs current implementation reveals:
- **Implemented**: 30% (Quick Reply basic, State Machine, AI Integration)
- **Partially Implemented**: 20% (Order workflow exists but lacks Draft Order system)
- **Not Implemented**: 50% (Nobita API, Email, Customer Tracking, Multi-tenant, Livestream, Human Handoff)

## Customer Requirements Analysis

### Requirement 1: Quick Reply cho 3 options
**Status**: DONE (70%)
**Implementation**: QuickReplyHandler service exists with ProductMappingService, GiftSelectionService, FreeshipCalculator
**Missing**: Need to configure 3 specific Quick Reply buttons in Facebook Page setup

### Requirement 2: Bot reply San Pham + Qua Tang, xin SDT + dia chi
**Status**: DONE (80%)
**Implementation**: QuickReplyHandler formats response with product + gift + freeship
**Missing**: Need to ensure state transition from QuickReply to ShippingAddress

### Requirement 3: Data cho AI
**Status**: PARTIAL (40%)
**Implementation**: Product data exists with RAG/vector search, AI prompt exists
**Missing**: FAQs, Promotions, Policies, Inventory sync, Shipping rules

### Requirement 4: Check ty le nhan hang tu Nobita
**Status**: NOT IMPLEMENTED (0%)
**Missing**: Nobita API client, Customer entity, Risk scoring, Tag system

### Requirement 5: Nhan dien khach cu/VIP
**Status**: NOT IMPLEMENTED (0%)
**Missing**: Customer entity, Segmentation, Order history tracking, Dynamic AI prompt

### Requirement 6: Prompt Ep Chot
**Status**: PARTIAL (30%)
**Implementation**: Current prompt has some sales focus
**Missing**: Aggressive ep chot system prompt, Objection handling

### Requirement 7: Draft Order System
**Status**: NOT IMPLEMENTED (0%)
**Missing**: DraftOrder workflow, SMTP email service, Email templates, Admin review interface

### Requirement 8: Human Handoff System
**Status**: NOT IMPLEMENTED (0%)
**Missing**: Handoff trigger, Email notification, Bot pause, Staff interface, Resume mechanism

### Requirement 9: Multi-Tenant Architecture
**Status**: NOT IMPLEMENTED (0%)
**Missing**: Tenant entity, Branch entity, TenantId columns, RLS policies, Tenant resolution

### Requirement 10: Livestream Auto-Reply
**Status**: NOT IMPLEMENTED (0%)
**Missing**: Facebook Live API handler, Comment detection, Auto-reply logic, Comment hiding

## Implementation Status Summary

### COMPLETED (30%)
1. Quick Reply Handler - 70% done
2. State Machine (17 states) - 100% done
3. AI Integration (Gemini) - 100% done
4. RAG/Vector Search - 100% done
5. Product catalog with gifts - 100% done

### IN PROGRESS (20%)
1. Quick Reply configuration
2. State transition refinement
3. AI prompt enhancement
4. Order workflow

### TO DO (50%)
1. Nobita API Integration
2. Draft Order + Email System
3. Customer Tracking & Risk Scoring
4. Human Handoff System
5. Multi-Tenant Architecture
6. Livestream Auto-Reply
7. AI Data Enhancement
8. Customer Segmentation

## Current State Machine Analysis

**Current States** (17 states): Idle, Greeting, MainMenu, BrowsingProducts, ProductDetail, SkinAnalysis, VariantSelection, AddToCart, CartReview, ShippingAddress, PaymentMethod, OrderConfirmation, OrderPlaced, OrderTracking, SkinConsultation, Help, Error

**Customer Requirement**: Simplified to 6 states - Idle, QuickReply, Consulting, CollectingInfo, DraftOrder, Complete

**Gap**: Current state machine is too complex for sales-focused flow. Need to refactor.

## Database Schema Gaps

### Missing Entities:
1. Customer - Track PSID, order history, risk score, segment
2. Tenant - Multi-tenant support
3. Branch - Facebook Page to tenant mapping
4. FAQ - Frequently asked questions for AI
5. Policy - Store policies, shipping rules
6. Promotion - Active promotions and campaigns
7. DraftOrder - Separate from Order for review workflow
8. HandoffSession - Track human handoff sessions
9. LivestreamSession - Track livestream events
10. CustomerTag - Tag system

### Missing Columns:
- All entities need tenant_id for multi-tenancy
- Customer needs delivery_success_rate, total_orders, lifetime_value
- Order needs nobita_order_id, approved_by, approved_at

## Unresolved Questions

1. Nobita API: Do we have API documentation? Authentication method?
2. Email Service: Use SendGrid/AWS SES or self-hosted SMTP?
3. Multi-Tenant: How many tenants expected? Shared DB or separate?
4. Livestream: Which Facebook API version?
5. Customer Risk Score: What is the formula?
6. Draft Order Approval: Web dashboard or email links?
7. State Machine Refactor: Migrate existing sessions or start fresh?
8. VIP Detection: Based on order count, revenue, or manual tagging?
