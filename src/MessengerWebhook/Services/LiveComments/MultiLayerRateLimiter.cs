using System.Collections.Concurrent;

namespace MessengerWebhook.Services.LiveComments;

/// <summary>
/// Multi-layer rate limiter for livestream comment automation
/// Implements per-video, per-user, and global rate limiting
/// </summary>
public class MultiLayerRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _videoTimestamps = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _userTimestamps = new();
    private readonly Queue<DateTime> _globalTimestamps = new();
    private readonly object _globalLock = new();

    private readonly int _maxRepliesPerVideo;
    private readonly int _maxRepliesPerUser;
    private readonly int _globalMaxRepliesPerMinute;

    public MultiLayerRateLimiter(
        int maxRepliesPerVideo,
        int maxRepliesPerUser,
        int globalMaxRepliesPerMinute)
    {
        _maxRepliesPerVideo = maxRepliesPerVideo;
        _maxRepliesPerUser = maxRepliesPerUser;
        _globalMaxRepliesPerMinute = globalMaxRepliesPerMinute;
    }

    public bool ShouldProcess(string videoId, string userId)
    {
        var now = DateTime.UtcNow;

        // Check global rate limit
        if (!CheckGlobalLimit(now))
        {
            return false;
        }

        // Check per-video rate limit
        if (!CheckVideoLimit(videoId, now))
        {
            return false;
        }

        // Check per-user rate limit
        if (!CheckUserLimit(userId, now))
        {
            return false;
        }

        // All checks passed, record timestamps
        RecordTimestamps(videoId, userId, now);
        return true;
    }

    private bool CheckGlobalLimit(DateTime now)
    {
        lock (_globalLock)
        {
            // Remove timestamps older than 1 minute
            while (_globalTimestamps.Count > 0 && (now - _globalTimestamps.Peek()).TotalMinutes > 1)
            {
                _globalTimestamps.Dequeue();
            }

            return _globalTimestamps.Count < _globalMaxRepliesPerMinute;
        }
    }

    private bool CheckVideoLimit(string videoId, DateTime now)
    {
        var queue = _videoTimestamps.GetOrAdd(videoId, _ => new Queue<DateTime>());

        lock (queue)
        {
            // Remove old timestamps (older than video lifetime, using 24 hours)
            while (queue.Count > 0 && (now - queue.Peek()).TotalHours > 24)
            {
                queue.Dequeue();
            }

            return queue.Count < _maxRepliesPerVideo;
        }
    }

    private bool CheckUserLimit(string userId, DateTime now)
    {
        var queue = _userTimestamps.GetOrAdd(userId, _ => new Queue<DateTime>());

        lock (queue)
        {
            // Remove timestamps older than 1 hour
            while (queue.Count > 0 && (now - queue.Peek()).TotalHours > 1)
            {
                queue.Dequeue();
            }

            return queue.Count < _maxRepliesPerUser;
        }
    }

    private void RecordTimestamps(string videoId, string userId, DateTime now)
    {
        // Record global timestamp
        lock (_globalLock)
        {
            _globalTimestamps.Enqueue(now);
        }

        // Record video timestamp
        var videoQueue = _videoTimestamps.GetOrAdd(videoId, _ => new Queue<DateTime>());
        lock (videoQueue)
        {
            videoQueue.Enqueue(now);
        }

        // Record user timestamp
        var userQueue = _userTimestamps.GetOrAdd(userId, _ => new Queue<DateTime>());
        lock (userQueue)
        {
            userQueue.Enqueue(now);
        }
    }

    public void Cleanup()
    {
        var now = DateTime.UtcNow;

        // Cleanup global timestamps
        lock (_globalLock)
        {
            while (_globalTimestamps.Count > 0 && (now - _globalTimestamps.Peek()).TotalMinutes > 1)
            {
                _globalTimestamps.Dequeue();
            }
        }

        // Cleanup video timestamps
        foreach (var kvp in _videoTimestamps)
        {
            var queue = kvp.Value;
            lock (queue)
            {
                while (queue.Count > 0 && (now - queue.Peek()).TotalHours > 24)
                {
                    queue.Dequeue();
                }
            }
        }

        // Cleanup user timestamps
        foreach (var kvp in _userTimestamps)
        {
            var queue = kvp.Value;
            lock (queue)
            {
                while (queue.Count > 0 && (now - queue.Peek()).TotalHours > 1)
                {
                    queue.Dequeue();
                }
            }
        }
    }
}
