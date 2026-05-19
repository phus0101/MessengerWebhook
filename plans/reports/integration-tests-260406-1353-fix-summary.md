# Integration Tests Fix Summary

**Date**: 2026-04-06  
**Status**: Partially Fixed  
**Result**: 82/126 tests passing (65%)

## 🎉 Fixes Completed

### 1. ProductEmbedding Vector Type Issue
**Problem**: InMemory database provider không hỗ trợ `Vector` type từ pgvector extension.

**Solution**: 
- Sửa `MessengerBotDbContext.cs` để ignore `ProductEmbedding` entity khi dùng InMemory provider
- Check `Database.ProviderName` và skip configuration nếu là InMemory

**Impact**: +60 tests passed

### 2. Missing Pinecone Configuration
**Problem**: Program.cs validate Pinecone API key khi startup nhưng tests không cung cấp.

**Solution**:
- Thêm Pinecone config vào `CustomWebApplicationFactory.cs`:
  - `Pinecone:ApiKey = "test_pinecone_api_key"`
  - `Pinecone:Environment = "test"`
  - `Pinecone:IndexName = "test-index"`

**Impact**: +22 tests passed

### 3. Load .env File in Tests
**Problem**: Tests chạy trong "Testing" environment nên không load `.env` file.

**Solution**:
- Thêm `DotNetEnv.Env.Load()` vào `CustomWebApplicationFactory.ConfigureWebHost()`

**Impact**: Config từ `.env` available trong tests

## ❌ Tests Còn Fail (40 tests)

### Category 1: Business Logic Changes (7 tests)
**Tests**:
- `ConversationFlowTests.ProcessMessage_ProductInterest_TransitionsToCollectingInfo`
- `ConversationFlowTests.StatePersistence_AcrossScopes_MaintainsSalesContext`
- `ConversationFlowTests.ProcessMessage_WithPhoneAndAddress_CreatesDraftAndCompletes`
- `ConversationFlowTests.ProcessMessage_ReturningCustomer_UsesRememberedContactWithSoftConfirmation`
- `ConversationFlowTests.ProcessMessage_ReturningCustomer_CanUpdateAddressWithoutReenteringPhone`
- `ReturningCustomerConfirmationTests.ReturningCustomer_ConfirmsWithOkEm_ShouldCreateDraftOrder`
- `ReturningCustomerConfirmationTests.ReturningCustomer_ConfirmsWithDungRoi_ShouldCreateDraftOrder`

**Root Cause**: State machine logic đã thay đổi. Tests expect state `CollectingInfo` nhưng actual là `Consulting`.

**Recommendation**: 
- **KHÔNG NÊN FIX** business logic vì có thể break production behavior
- **NÊN UPDATE** tests để match với behavior hiện tại
- Hoặc verify với team xem state transition mới có đúng không

### Category 2: Webhook Signature Issues (4 tests)
**Tests**:
- `LiveCommentWebhookTests.PostWebhook_WithMultipleFeedChanges_ProcessesAll`
- `LiveCommentWebhookTests.PostWebhook_WithBothMessagingAndFeedEvents_ProcessesBoth`
- `LiveCommentWebhookTests.PostWebhook_WithFeedCommentEvent_ProcessesSuccessfully`
- `LiveCommentWebhookTests.PostWebhook_WithNonLiveVideoComment_IgnoresComment`

**Root Cause**: Tests gửi webhook requests nhưng không generate valid Facebook signature, bị reject với `Unauthorized`.

**Recommendation**: Update tests để generate valid HMAC-SHA256 signature hoặc mock signature validation middleware.

### Category 3: Performance Tests (1 test)
**Test**: `VertexAIEmbeddingIntegrationTests.EmbedAsync_SingleProduct_MeetsLatencyRequirement`

**Root Cause**: Test expect latency < 200ms nhưng actual là 716ms (3.5× slower). Đây là real API call nên phụ thuộc network.

**Recommendation**: 
- Tăng timeout threshold lên 1000ms
- Hoặc skip test này trong CI/CD (mark as `[Fact(Skip = "Performance test")]`)

### Category 4: External Services (~28 tests)
**Tests**: RAG, VectorSearch, VietnameseBenchmark, BackgroundProcessing, WebhookEventEndpoint

**Root Cause**: Tests cần real external services:
- Pinecone index với data
- Vertex AI credentials
- Real Facebook webhook setup

**Recommendation**: 
- Mock external services trong tests
- Hoặc setup test environment với real services
- Hoặc mark as integration tests cần manual run

## 📊 Summary

| Category | Count | Status |
|----------|-------|--------|
| **Passed** | 82 | ✅ |
| Business Logic | 7 | ⚠️ Cần review |
| Webhook Signature | 4 | ⚠️ Cần mock |
| Performance | 1 | ⚠️ Cần adjust threshold |
| External Services | 28 | ⚠️ Cần mock/setup |
| **Skipped** | 4 | ⏭️ |
| **Total** | 126 | - |

## 🎯 Next Steps

### Priority 1: Review Business Logic (7 tests)
Verify với team xem state transitions mới có đúng không. Nếu đúng, update tests. Nếu sai, fix state machine logic.

### Priority 2: Fix Webhook Signature (4 tests)
Implement signature generation trong test helper hoặc mock SignatureValidationMiddleware.

### Priority 3: Adjust Performance Test (1 test)
Tăng latency threshold hoặc skip trong CI/CD.

### Priority 4: Mock External Services (28 tests)
Implement mocks cho Pinecone, Vertex AI, hoặc setup test environment.

## 📝 Files Modified

1. `src/MessengerWebhook/Data/MessengerBotDbContext.cs` - Ignore ProductEmbedding cho InMemory
2. `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs` - Add Pinecone config, load .env
