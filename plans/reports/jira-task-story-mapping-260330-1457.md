# Jira Task-Story Mapping

**Date**: 2026-03-30 14:57

## Phase 0: Foundation (Completed) - AG247-159 ✅

**Tasks (AG247-172 to AG247-177):**
- AG247-172: Setup PostgreSQL with pgvector ✅
- AG247-173: Implement Gemini AI Integration ✅
- AG247-174: Implement RAG Layer ✅
- AG247-175: Create Product Catalog entities ✅
- AG247-176: Implement 17-state State Machine ✅
- AG247-177: Create repositories and services ✅

## Phase 1: Quick Reply Handler (No Story created)

**Tasks (AG247-178 to AG247-181):**
- AG247-178: Create QuickReplyHandler ✅
- AG247-179: Update WebhookProcessor for Quick Reply ✅
- AG247-180: Add Quick Reply unit tests ✅
- AG247-181: Fix high-priority code review issues ✅
- AG247-182: Add auto-migration on startup

## Phase 1B: Quick Reply Configuration - AG247-160

**Tasks (AG247-183 to AG247-185):**
- AG247-183: Configure Facebook Page Quick Reply buttons
- AG247-184: Ensure state transition to CollectingInfo
- AG247-185: Test end-to-end Quick Reply flow

## Phase 2: Simplified State Machine (6 states) - AG247-161

**Tasks (AG247-186 to AG247-192):**
- AG247-186: Implement CollectingInfoStateHandler
- AG247-187: Implement DraftOrderStateHandler
- AG247-188: Implement CompleteStateHandler
- AG247-189: Update StateTransitionRules
- AG247-190: Create migration script for existing sessions
- AG247-191: Add state machine tests
- AG247-192: Remove old state handlers

## Phase 3: Ep Chot AI Prompt Enhancement - AG247-162

**Tasks (AG247-193 to AG247-197):**
- AG247-193: Design ep chot system prompt
- AG247-194: Implement prompt template system
- AG247-195: Add objection handling patterns
- AG247-196: Implement conversation memory
- AG247-197: Test ep chot effectiveness

## Phase 4: AI Data Enhancement - AG247-163

**Tasks (AG247-198 to AG247-207):**
- AG247-198: Create FAQ entity and repository
- AG247-199: Create Policy entity and repository
- AG247-200: Create Promotion entity and repository
- AG247-201: Create ShippingRule entity
- AG247-203: Implement RAG for FAQ search
- AG247-204: Implement RAG for Policy search
- AG247-205: Update AI prompt with knowledge base
- AG247-206: Seed initial data
- AG247-207: Add knowledge base tests

## Phase 5: Nobita CRM API Integration - AG247-164

**Tasks (AG247-208 to AG247-211, AG247-202):**
- AG247-208: Implement draft order creation
- AG247-209: Implement order approval
- AG247-210: Add Nobita API configuration
- AG247-211: Add Nobita API tests
- AG247-202: Add error handling and logging

## Phase 6: Customer Tracking & Risk Scoring - AG247-165

**Tasks (AG247-212 to AG247-215):**
- AG247-212: Create CustomerTag entity
- AG247-213: Implement auto-tagging logic
- AG247-214: Integrate with Nobita for order history
- AG247-215: Add customer tracking tests

## Phase 7: VIP Customer Detection & Tone Adjustment - AG247-166

**Tasks (AG247-216 to AG247-221):**
- AG247-216: Define VIP criteria
- AG247-217: Implement VIP detection service
- AG247-218: Create VIP-specific AI prompt
- AG247-219: Implement prompt switching logic
- AG247-220: Add strict no-discount rule
- AG247-221: Test VIP detection and tone

## Phase 8: Draft Order + Email Notification System - AG247-167

**Tasks (AG247-222 to AG247-225):**
- AG247-222: Create approval endpoint
- AG247-223: Implement Nobita order creation on approval
- AG247-224: Add email configuration
- AG247-225: Add draft order tests

## Phase 9: Human Handoff System - AG247-168

**Tasks (AG247-226 to AG247-229):**
- AG247-226: Create handoff completion endpoint
- AG247-227: Implement bot resume logic
- AG247-228: Add 30-minute timeout mechanism
- AG247-229: Add handoff tests

## Phase 10: Multi-Tenant Architecture - AG247-169

**Subtasks (not yet created):**
- Phase 10A: Database Schema Migration
- Phase 10B: Request Routing & Context
- Phase 10C: Caching Layer (Redis)
- Phase 10D: Security Hardening
- Phase 10E: Testing & Validation

## Phase 11: Livestream Auto-Reply - AG247-170

**Tasks (AG247-230):**
- AG247-230: Add livestream tests

## Phase 12: Testing & Production Deployment - AG247-171

**Tasks (AG247-231 to AG247-233):**
- AG247-231: Deploy to production
- AG247-232: Post-deployment monitoring
- AG247-233: Create runbook

---

## Cách tra cứu nhanh:

1. **Trong Jira UI**: Sử dụng JQL query
   ```
   project = AG247 AND parent = "AG247-159"
   ```

2. **Theo labels**: Mỗi task có labels chỉ phase
   ```
   project = AG247 AND labels = "phase-2"
   ```

3. **Xem file CSV gốc**: Cột "Parent" chứa tên Story
