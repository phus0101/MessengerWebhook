using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.LiveComments;

public class LiveCommentAutomationService : ILiveCommentAutomationService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IMessengerService _messengerService;
    private readonly ISessionManager _sessionManager;
    private readonly LiveCommentOptions _options;
    private readonly MultiLayerRateLimiter _rateLimiter;
    private readonly ILogger<LiveCommentAutomationService> _logger;

    public LiveCommentAutomationService(
        MessengerBotDbContext dbContext,
        IMessengerService messengerService,
        ISessionManager sessionManager,
        IOptions<LiveCommentOptions> options,
        ILogger<LiveCommentAutomationService> logger)
    {
        _dbContext = dbContext;
        _messengerService = messengerService;
        _sessionManager = sessionManager;
        _options = options.Value;
        _rateLimiter = new MultiLayerRateLimiter(
            _options.MaxRepliesPerVideo,
            _options.MaxRepliesPerUser,
            _options.GlobalMaxRepliesPerMinute);
        _logger = logger;
    }

    public Task<bool> ShouldHandleCommentAsync(string commentText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return Task.FromResult(false);

        // Check if comment contains trigger keywords
        var normalizedText = commentText.ToLowerInvariant();
        var hasKeyword = _options.TriggerKeywords
            .Any(keyword => normalizedText.Contains(keyword.ToLowerInvariant()));

        return Task.FromResult(hasKeyword);
    }

    public async Task ProcessCommentAsync(
        string commentId,
        string commenterPsid,
        string commentText,
        string videoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Multi-layer rate limiting check (per-video, per-user, global)
            if (!_rateLimiter.ShouldProcess(videoId, commenterPsid))
            {
                _logger.LogWarning(
                    "Rate limit exceeded for video {VideoId}, user {PSID}",
                    videoId,
                    commenterPsid);
                return;
            }

            // Idempotency check
            if (await IsCommentProcessedAsync(commentId, cancellationToken))
            {
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

            // Send public reply if enabled
            if (_options.EnablePublicReply && !string.IsNullOrWhiteSpace(_options.PublicReplyTemplate))
            {
                try
                {
                    await _messengerService.ReplyToCommentAsync(
                        commentId,
                        _options.PublicReplyTemplate,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reply to comment {CommentId}, continuing anyway", commentId);
                }
            }

            // Send welcome message with quick reply buttons (private message)
            var welcomeMessage = _options.WelcomeMessage;

            // Get top 3 products for quick reply buttons
            var topProducts = await _dbContext.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.CreatedAt)
                .Take(3)
                .Select(p => new { p.Code, p.Name })
                .ToListAsync(cancellationToken);

            if (topProducts.Any())
            {
                var quickReplies = topProducts
                    .Select(p => new QuickReplyButton("text", p.Name, $"PRODUCT_{p.Code}"))
                    .ToList();

                await _messengerService.SendQuickReplyAsync(
                    commenterPsid,
                    welcomeMessage,
                    quickReplies,
                    cancellationToken);
            }
            else
            {
                // Fallback to text-only if no products configured
                await _messengerService.SendTextMessageAsync(
                    commenterPsid,
                    welcomeMessage,
                    cancellationToken);
            }

            // Hide comment (best effort - don't fail if this fails)
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

            // Create conversation session
            await CreateConversationSessionAsync(commenterPsid, commentId, videoId, commentText, cancellationToken);

            _logger.LogInformation(
                "Processed livestream comment {CommentId} from {PSID}",
                commentId,
                commenterPsid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing comment {CommentId}", commentId);
            throw;
        }
    }

    private async Task<bool> IsCommentProcessedAsync(
        string commentId,
        CancellationToken cancellationToken)
    {
        var processed = await _dbContext.ConversationSessions
            .AnyAsync(x => x.ContextJson != null && x.ContextJson.Contains(commentId), cancellationToken);

        if (processed)
        {
            _logger.LogInformation("Comment {CommentId} already processed", commentId);
            return true;
        }

        return false;
    }

    private async Task CreateConversationSessionAsync(
        string commenterPsid,
        string commentId,
        string videoId,
        string commentText,
        CancellationToken cancellationToken)
    {
        var session = new ConversationSession
        {
            FacebookPSID = commenterPsid,
            CurrentState = ConversationState.Idle,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            ContextJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                sourceType = "livestream",
                sourceCommentId = commentId,
                sourceVideoId = videoId,
                sourceCommentText = commentText
            })
        };

        await _sessionManager.SaveAsync(session);
    }
}
