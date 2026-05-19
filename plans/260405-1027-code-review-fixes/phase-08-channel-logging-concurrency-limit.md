---
phase: 08
title: "H2/H3: Channel Drop Logging + Live Comment Concurrency Limit"
priority: P2 (High)
status: pending
depends_on: 07
---

## Overview
Add logging when Channel drops messages due to capacity overflow, and add concurrency limits for Facebook Live comment handling.

## Files to Modify
- `src/MessengerWebhook/Program.cs` (lines 362-367, 505-529)
- `src/MessengerWebhook/Services/LiveCommentMonitoringService.cs` (new)

## Implementation Steps

### H2: Channel Drop Logging

1. **Wrap channel writer with monitoring**
   - Replace `Channel.CreateBounded<MessagingEvent>` with custom wrapper
   - Or use `ChannelOptions.SingleWriter = true` with a monitoring loop
   - Add background service that polls channel count periodically (`channel.Reader.Count`)
   - Log warning when count exceeds 80% capacity: `_logger.LogWarning("Event channel at {Count}/1000, approaching capacity")`
   - Log error when drops actually occur: `_logger.LogError("Channel overflow - dropped event for PSID {PSID}")`

2. **Consider changing FullMode** (optional, evaluate during implementation)
   - If message loss is unacceptable for business, switch from `DropOldest` to `DropNewest` (newer events more likely duplicates that can be recovered)
   - Or use `BoundedChannelFullMode.Wait` with timeout + alerting

### H3: Live Comment Concurrency Limit

1. **Add SemaphoreSlim to Program.cs**
   - Replace fire-and-forget `_ = Task.Run(async () => { ... })` with semaphore-guarded execution:
   ```csharp
   var liveCommentSemaphore = new SemaphoreSlim(50); // max 50 concurrent handlers
   // ...
   _ = Task.Run(async () => {
       await liveCommentSemaphore.WaitAsync(ct);
       try {
           await HandleLiveCommentAsync(comment, ct);
       } finally {
           liveCommentSemaphore.Release();
       }
   }, ct);
   ```

2. **Add monitoring** (optional)
   - Log when semaphore queue depth is high: "Live comment handler backlogged: {CurrentCount}/50 in use"

3. **Register semaphore as singleton** in DI for access from wherever live comments are processed

## Success Criteria
- Warning logs when channel approaches capacity
- Error logs when messages are actually dropped
- Live comment handling bounded to max 50 concurrent
- No ThreadPool exhaustion during high-traffic live events
- `dotnet build` succeeds, tests pass

## Risk Assessment
- **Likelihood:** Low
- **Impact:** Low - adding logging and limits, not removing features
- **Mitigation:** Semaphore limit of 50 is generous for typical Facebook Live usage

## Rollback
Revert commit. Logging additions are safe to remove; semaphore limit can be temporarily increased.
