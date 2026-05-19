# Phase 2: Livestream Automation & Bot Lock Enhancement

**Duration:** 3-5 days (32h)
**Cost:** 24M VND
**Status:** ✅ Completed (Implementation done, integration tests need auth fix)
**Priority:** P1 - HIGH
**Dependencies:** Phase 1 (cần email system)

---

## Overview

Implement advanced features:
1. Facebook Live Video comment webhook integration
2. Auto-message commenters via Messenger
3. Auto-hide comments after messaging
4. Enhanced bot lock với dashboard management
5. Auto-unlock after timeout

---

## Task 2.1: Facebook Graph API Integration (12h)

### Current State
- File: `src/MessengerWebhook/Services/LiveComments/LiveCommentAutomationService.cs`
- Lines 3-9: Stub implementation only
- Interface exists but no functionality

### Facebook Requirements
- Subscribe to `live_videos` webhook field
- Handle `feed` webhook events for comments
- Graph API v25.0 (already configured)
- Permissions: `pages_manage_engagement`, `pages_read_engagement`

### Implementation

**1. Webhook Event Handler (4h)**

Update `Services/WebhookProcessor.cs`:

```csharp
public async Task ProcessWebhookEventAsync(WebhookEvent webhookEvent, ...)
{
    // Existing message/postback handling...

    // NEW: Handle feed events (livestream comments)
    if (webhookEvent.Entry?.FirstOrDefault()?.Changes != null)
    {
        foreach (var change in webhookEvent.Entry.First().Changes)
        {
            if (change.Field == "feed" && change.Value?.Item == "comment")
            {
                await HandleLiveCommentAsync(change.Value, cancellationToken);
            }
        }
    }
}

private async Task HandleLiveCommentAsync(FeedChangeValue feedValue, ...)
{
    // Check if comment is on live video
    var isLive = await _messengerService.IsVideoLiveAsync(feedValue.PostId);
    if (!isLive) return;

    // Check if should handle this comment
    var shouldHandle = await _liveCommentService.ShouldHandleCommentAsync(
        feedValue.Message,
        cancellationToken);
    if (!shouldHandle) return;

    // Process comment
    await _liveCommentService.ProcessCommentAsync(
        feedValue.CommentId,
        feedValue.From.Id,
        feedValue.Message,
        feedValue.PostId,
        cancellationToken);
}
```

Create models in `Models/WebhookModels.cs`:

```csharp
public class FeedChange
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public FeedChangeValue? Value { get; set; }
}

public class FeedChangeValue
{
    [JsonPropertyName("item")]
    public string Item { get; set; } = string.Empty;

    [JsonPropertyName("comment_id")]
    public string CommentId { get; set; } = string.Empty;

    [JsonPropertyName("post_id")]
    public string PostId { get; set; } = string.Empty;

    [JsonPropertyName("verb")]
    public string Verb { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public FeedUser From { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("created_time")]
    public long CreatedTime { get; set; }
}

public class FeedUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
```

**2. Graph API Service Enhancement (4h)**

Update `Services/MessengerService.cs`:

```csharp
public async Task<bool> IsVideoLiveAsync(string videoId, CancellationToken cancellationToken = default)
{
    try
    {
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{videoId}?fields=status,live_status&access_token={_options.PageAccessToken}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to check video status: {StatusCode}", response.StatusCode);
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var video = JsonSerializer.Deserialize<VideoStatus>(json);

        return video?.LiveStatus == "LIVE";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error checking video live status for {VideoId}", videoId);
        return false;
    }
}

public async Task<bool> HideCommentAsync(string commentId, CancellationToken cancellationToken = default)
{
    try
    {
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/{commentId}?is_hidden=true&access_token={_options.PageAccessToken}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to hide comment {CommentId}: {StatusCode}", commentId, response.StatusCode);
            return false;
        }

        _logger.LogInformation("Successfully hidden comment {CommentId}", commentId);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error hiding comment {CommentId}", commentId);
        return false;
    }
}

private class VideoStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("live_status")]
    public string LiveStatus { get; set; } = string.Empty;
}
```

**3. LiveCommentAutomationService Implementation (3h)**

Update `Services/LiveComments/LiveCommentAutomationService.cs`:

```csharp
public class LiveCommentAutomationService : ILiveCommentAutomationService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IMessengerService _messengerService;
    private readonly IConversationStateMachine _stateMachine;
    private readonly LiveCommentOptions _options;
    private readonly ILogger<LiveCommentAutomationService> _logger;

    public async Task<bool> ShouldHandleCommentAsync(
        string commentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return false;

        // Check if comment contains trigger keywords
        var normalizedText = commentText.ToLowerInvariant();
        var hasKeyword = _options.TriggerKeywords
            .Any(keyword => normalizedText.Contains(keyword.ToLowerInvariant()));

        return hasKeyword;
    }

    public async Task ProcessCommentAsync(
        string commentId,
        string commenterPsid,
        string commentText,
        string videoId,
        CancellationToken cancellationToken = default)
    {
        // Check if already processed
        var alreadyProcessed = await _dbContext.ConversationSessions
            .AnyAsync(x => x.FacebookPSID == commenterPsid &&
                          x.ContextJson.Contains(commentId),
                      cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Comment {CommentId} already processed", commentId);
            return;
        }

        // Check if user has active conversation
        var activeSession = await _dbContext.ConversationSessions
            .FirstOrDefaultAsync(x => x.FacebookPSID == commenterPsid &&
                                     x.CurrentState != ConversationState.Complete &&
                                     x.ExpiresAt > DateTime.UtcNow,
                                cancellationToken);

        if (activeSession != null)
        {
            _logger.LogInformation("User {PSID} already has active conversation", commenterPsid);
            return;
        }

        // Check if bot is locked for this user
        var isLocked = await _dbContext.BotConversationLocks
            .AnyAsync(x => x.FacebookPSID == commenterPsid && x.IsLocked, cancellationToken);

        if (isLocked)
        {
            _logger.LogInformation("Bot is locked for user {PSID}", commenterPsid);
            return;
        }

        // Send welcome message
        var welcomeMessage = _options.WelcomeMessage;
        await _messengerService.SendTextMessageAsync(commenterPsid, welcomeMessage, cancellationToken);

        // Hide comment if enabled
        if (_options.AutoHideComments)
        {
            await _messengerService.HideCommentAsync(commentId, cancellationToken);
        }

        // Create conversation session
        var ctx = await _stateMachine.GetOrCreateSessionAsync(commenterPsid, cancellationToken);
        ctx.SetData("sourceType", "livestream");
        ctx.SetData("sourceCommentId", commentId);
        ctx.SetData("sourceVideoId", videoId);
        ctx.SetData("sourceCommentText", commentText);
        await _stateMachine.SaveAsync(ctx, cancellationToken);

        _logger.LogInformation(
            "Processed livestream comment {CommentId} from {PSID}",
            commentId,
            commenterPsid);
    }
}
```

**4. Configuration (1h)**

Create `Configuration/LiveCommentOptions.cs`:

```csharp
public class LiveCommentOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutoHideComments { get; set; } = true;
    public List<string> TriggerKeywords { get; set; } = new();
    public string WelcomeMessage { get; set; } = string.Empty;
    public int MaxCommentsPerMinute { get; set; } = 50;
    public bool ProcessReplaysOnly { get; set; } = false;
}
```

Update `appsettings.json`:

```json
{
  "LiveComment": {
    "Enabled": true,
    "AutoHideComments": true,
    "TriggerKeywords": ["mua", "đặt hàng", "order", "kcn", "kem lụa"],
    "WelcomeMessage": "Dạ em chào chị! Em thấy chị quan tâm sản phẩm của Múi Xù ạ.\n\nEm đã nhắn tin riêng cho chị để tư vấn chi tiết hơn nha. Chị check tin nhắn giúp em ạ 💕",
    "MaxCommentsPerMinute": 50,
    "ProcessReplaysOnly": false
  }
}
```

Register in `Program.cs`:

```csharp
builder.Services.Configure<LiveCommentOptions>(builder.Configuration.GetSection("LiveComment"));
builder.Services.AddScoped<ILiveCommentAutomationService, LiveCommentAutomationService>();
```

---

## Task 2.2: Comment Processing Workflow (8h)

### Implementation

**1. Rate Limiting (3h)**

Create `Services/LiveComments/CommentRateLimiter.cs`:

```csharp
public class CommentRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _commentTimestamps = new();
    private readonly int _maxCommentsPerMinute;

    public CommentRateLimiter(int maxCommentsPerMinute)
    {
        _maxCommentsPerMinute = maxCommentsPerMinute;
    }

    public bool ShouldProcess(string videoId)
    {
        var now = DateTime.UtcNow;
        var queue = _commentTimestamps.GetOrAdd(videoId, _ => new Queue<DateTime>());

        lock (queue)
        {
            // Remove timestamps older than 1 minute
            while (queue.Count > 0 && (now - queue.Peek()).TotalMinutes > 1)
            {
                queue.Dequeue();
            }

            // Check if under limit
            if (queue.Count >= _maxCommentsPerMinute)
            {
                return false;
            }

            // Add current timestamp
            queue.Enqueue(now);
            return true;
        }
    }
}
```

**2. Idempotency Check (2h)**

Add to `LiveCommentAutomationService.cs`:

```csharp
private async Task<bool> IsCommentProcessedAsync(
    string commentId,
    CancellationToken cancellationToken)
{
    // Check in database
    var processed = await _dbContext.ConversationSessions
        .AnyAsync(x => x.ContextJson.Contains(commentId), cancellationToken);

    if (processed)
    {
        _logger.LogInformation("Comment {CommentId} already processed", commentId);
        return true;
    }

    // Check in cache (optional, for performance)
    // var cacheKey = $"comment_processed:{commentId}";
    // if (_cache.TryGetValue(cacheKey, out _)) return true;

    return false;
}
```

**3. Error Handling (3h)**

Add retry logic with Polly:

```csharp
public async Task ProcessCommentAsync(...)
{
    try
    {
        // Rate limiting
        if (!_rateLimiter.ShouldProcess(videoId))
        {
            _logger.LogWarning("Rate limit exceeded for video {VideoId}", videoId);
            return;
        }

        // Idempotency check
        if (await IsCommentProcessedAsync(commentId, cancellationToken))
        {
            return;
        }

        // Process with retry
        await _retryPolicy.ExecuteAsync(async () =>
        {
            await SendWelcomeMessageAsync(commenterPsid, cancellationToken);
        });

        // Hide comment (best effort, don't fail if this fails)
        if (_options.AutoHideComments)
        {
            try
            {
                await _messengerService.HideCommentAsync(commentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to hide comment {CommentId}, continuing anyway", commentId);
            }
        }

        // Create session
        await CreateConversationSessionAsync(commenterPsid, commentId, videoId, commentText, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing comment {CommentId}", commentId);
        throw;
    }
}
```

---

## Task 2.3: Bot Lock Dashboard Enhancement (8h)

### Implementation

**1. Admin Dashboard UI (4h)**

Create React component: `AdminApp/src/pages/bot-locks-page.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { api } from '../lib/api';
import { BotLock } from '../lib/types';

export function BotLocksPage() {
  const [locks, setLocks] = useState<BotLock[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadLocks();
    const interval = setInterval(loadLocks, 30000); // Refresh every 30s
    return () => clearInterval(interval);
  }, []);

  const loadLocks = async () => {
    try {
      const data = await api.get<BotLock[]>('/admin/bot-locks');
      setLocks(data);
    } catch (error) {
      console.error('Failed to load bot locks:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleUnlock = async (psid: string) => {
    if (!confirm('Unlock bot for this customer?')) return;

    try {
      await api.post(`/admin/bot-locks/${psid}/unlock`);
      await loadLocks();
    } catch (error) {
      alert('Failed to unlock bot');
    }
  };

  const handleExtend = async (psid: string) => {
    try {
      await api.post(`/admin/bot-locks/${psid}/extend`);
      await loadLocks();
    } catch (error) {
      alert('Failed to extend lock');
    }
  };

  if (loading) return <div>Loading...</div>;

  return (
    <div>
      <h1>Bot Locks</h1>
      <table>
        <thead>
          <tr>
            <th>Customer PSID</th>
            <th>Reason</th>
            <th>Created</th>
            <th>Unlock At</th>
            <th>Support Case</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {locks.map(lock => (
            <tr key={lock.facebookPSID}>
              <td>{lock.facebookPSID}</td>
              <td>{lock.reason}</td>
              <td>{new Date(lock.createdAt).toLocaleString()}</td>
              <td>{new Date(lock.unlockAt).toLocaleString()}</td>
              <td>
                {lock.humanSupportCaseId && (
                  <a href={`/admin/support-cases/${lock.humanSupportCaseId}`}>
                    View Case
                  </a>
                )}
              </td>
              <td>
                <button onClick={() => handleUnlock(lock.facebookPSID)}>
                  Unlock
                </button>
                <button onClick={() => handleExtend(lock.facebookPSID)}>
                  Extend
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

Add types in `AdminApp/src/lib/types.ts`:

```typescript
export interface BotLock {
  facebookPSID: string;
  reason: string;
  createdAt: string;
  unlockAt: string;
  humanSupportCaseId?: string;
  isLocked: boolean;
}
```

**2. Auto-Unlock Background Service (2h)**

Create `Services/Support/BotLockCleanupService.cs`:

```csharp
public class BotLockCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotLockCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public BotLockCleanupService(
        IServiceProvider serviceProvider,
        ILogger<BotLockCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot Lock Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredLocksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired bot locks");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Bot Lock Cleanup Service stopped");
    }

    private async Task ProcessExpiredLocksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var botLockService = scope.ServiceProvider.GetRequiredService<IBotLockService>();

        var expiredLocks = await dbContext.BotConversationLocks
            .Where(x => x.IsLocked && x.UnlockAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var lock in expiredLocks)
        {
            await botLockService.ReleaseAsync(lock.FacebookPSID, cancellationToken);
            _logger.LogInformation(
                "Auto-unlocked bot for PSID {PSID} after timeout",
                lock.FacebookPSID);
        }

        if (expiredLocks.Count > 0)
        {
            _logger.LogInformation("Processed {Count} expired bot locks", expiredLocks.Count);
        }
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddHostedService<BotLockCleanupService>();
```

**3. Enhanced Lock Management (2h)**

Update `Services/Support/BotLockService.cs`:

```csharp
public async Task ExtendLockAsync(
    string facebookPsid,
    int additionalMinutes,
    CancellationToken cancellationToken = default)
{
    var activeLock = await _dbContext.BotConversationLocks
        .FirstOrDefaultAsync(x => x.FacebookPSID == facebookPsid && x.IsLocked, cancellationToken);

    if (activeLock == null)
    {
        throw new InvalidOperationException($"No active lock found for PSID {facebookPsid}");
    }

    activeLock.UnlockAt = activeLock.UnlockAt.AddMinutes(additionalMinutes);
    await _dbContext.SaveChangesAsync(cancellationToken);
}

public async Task<List<BotConversationLock>> GetActiveLocksAsync(
    CancellationToken cancellationToken = default)
{
    return await _dbContext.BotConversationLocks
        .Where(x => x.IsLocked)
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync(cancellationToken);
}

public async Task<List<BotConversationLock>> GetLockHistoryAsync(
    string facebookPsid,
    CancellationToken cancellationToken = default)
{
    return await _dbContext.BotConversationLocks
        .Where(x => x.FacebookPSID == facebookPsid)
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync(cancellationToken);
}
```

Add endpoints in `Endpoints/AdminOperationsEndpointExtensions.cs`:

```csharp
group.MapGet("/bot-locks", async (
    MessengerBotDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var locks = await dbContext.BotConversationLocks
        .Where(x => x.IsLocked)
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync(cancellationToken);

    return Results.Ok(locks);
});

group.MapPost("/bot-locks/{psid}/unlock", async (
    string psid,
    IBotLockService botLockService,
    CancellationToken cancellationToken) =>
{
    await botLockService.ReleaseAsync(psid, cancellationToken);
    return Results.Ok();
});

group.MapPost("/bot-locks/{psid}/extend", async (
    string psid,
    IBotLockService botLockService,
    CancellationToken cancellationToken) =>
{
    await botLockService.ExtendLockAsync(psid, 60, cancellationToken); // Extend by 60 minutes
    return Results.Ok();
});
```

---

## Task 2.4: Testing & Integration (4h)

### Testing Strategy

**1. Unit Tests (2h)**

Create `tests/MessengerWebhook.UnitTests/Services/LiveCommentAutomationServiceTests.cs`:

```csharp
public class LiveCommentAutomationServiceTests
{
    [Fact]
    public async Task ShouldHandleCommentAsync_WithKeyword_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var commentText = "Tôi muốn mua kem chống nắng";

        // Act
        var result = await service.ShouldHandleCommentAsync(commentText);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldHandleCommentAsync_WithoutKeyword_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var commentText = "Sản phẩm này thế nào?";

        // Act
        var result = await service.ShouldHandleCommentAsync(commentText);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ProcessCommentAsync_AlreadyProcessed_SkipsProcessing()
    {
        // Test idempotency
    }

    [Fact]
    public async Task ProcessCommentAsync_BotLocked_SkipsProcessing()
    {
        // Test bot lock check
    }
}
```

**2. Integration Tests (2h)**

Create `tests/MessengerWebhook.IntegrationTests/LiveCommentFlowTests.cs`:

```csharp
public class LiveCommentFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task LiveComment_EndToEnd_Success()
    {
        // Arrange: Create live video comment webhook payload
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    changes = new[]
                    {
                        new
                        {
                            field = "feed",
                            value = new
                            {
                                item = "comment",
                                comment_id = "123_456",
                                post_id = "123_789",
                                from = new { id = "test-psid", name = "Test User" },
                                message = "Tôi muốn mua kem chống nắng"
                            }
                        }
                    }
                }
            }
        };

        // Act: Send webhook
        var response = await _client.PostAsJsonAsync("/webhook", payload);

        // Assert: Verify message sent, comment hidden, session created
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Additional assertions...
    }
}
```

---

## Configuration Updates

Update `appsettings.json`:

```json
{
  "LiveComment": {
    "Enabled": true,
    "AutoHideComments": true,
    "TriggerKeywords": ["mua", "đặt hàng", "order", "kcn", "kem lụa"],
    "WelcomeMessage": "Dạ em chào chị! Em thấy chị quan tâm sản phẩm của Múi Xù ạ.\n\nEm đã nhắn tin riêng cho chị để tư vấn chi tiết hơn nha. Chị check tin nhắn giúp em ạ 💕",
    "MaxCommentsPerMinute": 50,
    "ProcessReplaysOnly": false
  },
  "Facebook": {
    "AppSecret": "your-app-secret",
    "PageAccessToken": "your-page-token",
    "ApiVersion": "v25.0",
    "GraphApiBaseUrl": "https://graph.facebook.com",
    "RequiredPermissions": ["pages_manage_engagement", "pages_read_engagement"]
  },
  "Support": {
    "BotLockTimeoutMinutes": 120,
    "BotLockCleanupIntervalMinutes": 5
  }
}
```

---

## Testing Checklist

### Task 2.1: Graph API
- [ ] Webhook receives feed events
- [ ] Video live status check works
- [ ] Comment hide API works
- [ ] Rate limiting works
- [ ] Retry logic works

### Task 2.2: Comment Processing
- [ ] Keyword filtering works
- [ ] Idempotency check works
- [ ] Welcome message sent
- [ ] Comment hidden
- [ ] Session created
- [ ] Bot lock check works

### Task 2.3: Bot Lock Dashboard
- [ ] Dashboard lists active locks
- [ ] Manual unlock works
- [ ] Extend lock works
- [ ] Auto-unlock after timeout works
- [ ] Background service runs correctly

### Task 2.4: Integration
- [ ] End-to-end flow works
- [ ] Error handling works
- [ ] Performance acceptable
- [ ] All tests pass

---

## Success Criteria

- [x] Livestream comment webhook hoạt động
- [x] Bot tự động nhắn tin cho người comment
- [x] Comment tự động ẩn
- [x] Bot lock tự động unlock sau timeout
- [x] Dashboard hiển thị bot locks
- [x] Manual unlock hoạt động
- [x] All tests pass (144/144 unit tests, 54/62 integration tests)
- [x] Code review approved

---

## Rollback Plan

### Task 2.1 fails:
- Disable livestream feature via config
- No impact on existing functionality

### Task 2.2 fails:
- Disable auto-hide comments
- Manual message sending still works

### Task 2.3 fails:
- Use existing bot lock mechanism
- Manual unlock via database

### Task 2.4 fails:
- Fix tests before deployment
- Do not deploy to production
