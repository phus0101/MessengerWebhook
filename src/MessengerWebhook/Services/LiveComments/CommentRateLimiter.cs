using System.Collections.Concurrent;

namespace MessengerWebhook.Services.LiveComments;

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
