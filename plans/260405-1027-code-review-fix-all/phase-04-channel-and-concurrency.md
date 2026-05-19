# Phase 4: Channel Drop Logging + Live Comment Concurrency (H2, H3)

## Overview
- Priority: High
- Current status: Not started
- Effort: 1h
- Issues: H2 (Channel silently drops messages), H3 (Unbounded Task.Run for live comments)

## H2 Problem: Channel DropOldest Silently Loses Messages
Channel capacity 1000 with `DropOldest` mode. When traffic spikes >1000 pending events, messages drop without any log or alert.

## H3 Problem: Unbounded Task.Run for Live Comments
Each Facebook Live comment spawns an unbounded `Task.Run` in Program.cs (lines 505-529). Popular lives with hundreds of comments/minute can cause ThreadPool exhaustion.

## Context Links
- `src/MessengerWebhook/Program.cs:362-367` (channel config)
- `src/MessengerWebhook/Program.cs:505-529` (live comment Task.Run)
- `src/MessengerWebhook/BackgroundServices/WebhookProcessingService.cs` (channel consumer)

## Architecture

### H2 Fix: Channel Drop Monitoring
Add a counter + periodic warning:
```csharp
// Wrap channel writer with monitoring
var dropCounter = 0;
var lastWarningTime = DateTime.MinValue;
var channel = Channel.CreateBounded<MessagingEvent>(
    new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = false,
        SingleReader = true,
    });
```

Add a background check that logs when queue is >80% capacity:
```csharp
// In WebhookProcessingService or a new monitor
if (channel.Reader.Count > 800 && DateTime.UtcNow - lastWarningTime > TimeSpan.FromSeconds(30))
{
    logger.LogWarning("Channel queue at {Count}/1000 - approaching capacity. Consider scaling.", channel.Reader.Count);
    lastWarningTime = DateTime.UtcNow;
}
```

For actual drop detection: use a custom wrapper or switch to a custom `DropOnWrite` callback (not built-in). Simpler approach: wrap writer with capacity check.

### H3 Fix: SemaphoreSlim for Live Comments
Replace `Task.Run` with bounded concurrency:
```csharp
var liveCommentSemaphore = new SemaphoreSlim(50);
// In webhook handler:
_ = Task.Run(async () =>
{
    await liveCommentSemaphore.WaitAsync();
    try { /* process comment */ }
    finally { liveComment_semaphore.Release(); }
});
```

Better: route live comments through the same `Channel<T>` used for messaging events — single processing path.

## Implementation Steps

### Step 1: Add channel capacity monitoring (H2)

In `Program.cs`, wrap channel registration:
```csharp
// Register channel with monitoring
var channelOptions = new BoundedChannelOptions(1000)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleWriter = false,
    SingleReader = true,
};
var channel = Channel.CreateBounded<MessagingEvent>(channelOptions);
builder.Services.AddSingleton(channel);

// Add a background monitor
builder.Services.AddHostedService(sp =>
    new ChannelMonitorService(channel, sp.GetRequiredService<ILogger<ChannelMonitorService>>()));
```

Create `src/MessengerWebhook/BackgroundServices/ChannelMonitorService.cs`:
- Runs every 30s
- Logs warning if queue > 80% capacity
- Logs critical if queue > 95%

### Step 2: Add drop detection wrapper (H2)

Create `src/MessengerWebhook/Services/InstrumentedChannelWriter.cs`:
```csharp
public class InstrumentedChannelWriter
{
    private readonly Channel<MessagingEvent> _channel;
    private readonly ILogger _logger;
    private int _totalDropped;

    public async Task<bool> WriteAsync(MessagingEvent evt, CancellationToken ct)
    {
        if (_channel.Writer.TryWrite(evt)) return true;

        // Channel at capacity — try to write anyway (will drop with DropOldest)
        // The BoundedChannelFullMode.DropOldest handles it silently,
        // but we want to log when this happens
        Interlocked.Increment(ref _totalDropped);
        var written = await _channel.Writer.WriteAsync(evt, ct);
        if (_totalDropped % 100 == 0)
        {
            _logger.LogWarning("Channel dropped {Dropped} messages total. Current depth: {Depth}", _totalDropped, _channel.Reader.Count);
        }
        return written;
    }
}
```

Actually, simpler approach: since `DropOldest` handles it silently without `TryWrite` returning false, the practical fix is the monitoring service. The monitor's warning at 80% capacity gives ops team time to react before drops occur.

### Step 3: Add SemaphoreSlim for live comments (H3)

In `Program.cs`, before `app.Run()`:
```csharp
var liveCommentConcurrency = new SemaphoreSlim(50, 50);
builder.Services.AddSingleton(liveCommentConcurrency);
```

Update the live comment handler:
```csharp
_ = Task.Run(async () =>
{
    await liveCommentConcurrency.WaitAsync();
    try
    {
        // ... existing processing code
    }
    finally
    {
        liveCommentConcurrency.Release();
    }
});
```

Add logging when semaphore wait exceeds threshold:
```csharp
var waitStart = DateTime.UtcNow;
await liveCommentConcurrency.WaitAsync();
var waitMs = (DateTime.UtcNow - waitStart).TotalMilliseconds;
if (waitMs > 1000)
{
    logger.LogWarning("Live comment processing delayed {WaitMs}ms waiting for concurrency slot", waitMs);
}
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/BackgroundServices/ChannelMonitorService.cs`

**To modify:**
- `src/MessengerWebhook/Program.cs` (channel config, live comment handler, SemaphoreSlim registration)

## Todo List

- [ ] Create ChannelMonitorService.cs
- [ ] Register ChannelMonitorService as hosted service
- [ ] Add capacity monitoring to channel config (80% warning log)
- [ ] Add SemaphoreSlim for live comment concurrency (max 50)
- [ ] Add semaphore wait time logging
- [ ] Run dotnet build

## Success Criteria

- ChannelMonitorService logs warning when queue > 80% capacity
- Live comment processing limited to 50 concurrent handlers
- No ThreadPool exhaustion during high-traffic Facebook Live events
- `dotnet build` succeeds

## Risk Assessment

**Low risk.** Channel monitoring is read-only. SemaphoreSlim for live comments may slow response under extreme load, but this prevents ThreadPool exhaustion — an acceptable tradeoff. The 50 limit can be tuned via configuration.
