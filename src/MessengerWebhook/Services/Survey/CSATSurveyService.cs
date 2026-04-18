using System.Collections.Concurrent;
using System.Globalization;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Survey;

public class CSATSurveyService : ICSATSurveyService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IMessengerService _messengerService;
    private readonly ITenantContext _tenantContext;
    private readonly CSATSurveyOptions _options;
    private readonly ILogger<CSATSurveyService> _logger;

    // Track users awaiting feedback (thread-safe)
    private static readonly ConcurrentDictionary<string, byte> _awaitingFeedback = new();

    public CSATSurveyService(
        MessengerBotDbContext dbContext,
        IMessengerService messengerService,
        ITenantContext tenantContext,
        IOptions<CSATSurveyOptions> options,
        ILogger<CSATSurveyService> logger)
    {
        _dbContext = dbContext;
        _messengerService = messengerService;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendSurveyAsync(string sessionId)
    {
        try
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("CSAT survey disabled in configuration");
                return;
            }

            var session = await _dbContext.ConversationSessions
                .Where(s => s.TenantId == _tenantContext.TenantId)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found for CSAT survey", sessionId);
                return;
            }

            // Check if survey already sent
            if (session.SurveySent)
            {
                _logger.LogInformation("Survey already sent for session {SessionId}", sessionId);
                return;
            }

            // Check if session is still in Complete state
            if (session.CurrentState != ConversationState.Complete)
            {
                _logger.LogInformation("Session {SessionId} no longer in Complete state, skipping survey", sessionId);
                return;
            }

            // Send survey with quick reply buttons
            var quickReplies = new List<QuickReplyButton>
            {
                new("text", "⭐", "CSAT_RATING_1"),
                new("text", "⭐⭐", "CSAT_RATING_2"),
                new("text", "⭐⭐⭐", "CSAT_RATING_3"),
                new("text", "⭐⭐⭐⭐", "CSAT_RATING_4"),
                new("text", "⭐⭐⭐⭐⭐", "CSAT_RATING_5")
            };

            await _messengerService.SendQuickReplyAsync(
                session.FacebookPSID,
                _options.Messages.SurveyQuestion,
                quickReplies);

            // Mark survey as sent
            session.SurveySent = true;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("CSAT survey sent to session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending CSAT survey for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task HandleRatingAsync(string psid, int rating)
    {
        try
        {
            if (rating < 1 || rating > 5)
            {
                _logger.LogWarning("Invalid rating {Rating} from PSID {PSID}", rating, psid);
                return;
            }

            var session = await _dbContext.ConversationSessions
                .Where(s => s.TenantId == _tenantContext.TenantId)
                .FirstOrDefaultAsync(s => s.FacebookPSID == psid);

            if (session == null)
            {
                _logger.LogWarning("Session not found for PSID {PSID}", psid);
                return;
            }

            // Check if survey already exists
            var existingSurvey = await _dbContext.ConversationSurveys
                .FirstOrDefaultAsync(s => s.SessionId == session.Id);

            if (existingSurvey != null)
            {
                _logger.LogInformation("Survey already exists for session {SessionId}, ignoring duplicate", session.Id);
                return;
            }

            // Create survey record
            var survey = new ConversationSurvey
            {
                SessionId = session.Id,
                FacebookPsid = psid,
                TenantId = session.TenantId,
                ABTestVariant = session.ABTestVariant,
                Rating = rating,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ConversationSurveys.Add(survey);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("CSAT rating {Rating} saved for session {SessionId}", rating, session.Id);

            // Send follow-up for low ratings
            if (_options.SendFollowUpForLowRatings && rating <= _options.LowRatingThreshold)
            {
                await _messengerService.SendTextMessageAsync(psid, _options.Messages.FollowUpQuestion);
                _awaitingFeedback.TryAdd(psid, 0);
                _logger.LogInformation("Follow-up question sent to PSID {PSID} for low rating", psid);
            }
            else
            {
                // Send thank you message for high ratings
                await _messengerService.SendTextMessageAsync(psid, _options.Messages.ThankYou);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling CSAT rating for PSID {PSID}", psid);
            throw;
        }
    }

    public async Task HandleFeedbackAsync(string psid, string feedbackText)
    {
        try
        {
            // Check if user is awaiting feedback
            if (!_awaitingFeedback.ContainsKey(psid))
            {
                _logger.LogDebug("PSID {PSID} not awaiting feedback, ignoring message", psid);
                return;
            }

            var session = await _dbContext.ConversationSessions
                .Where(s => s.TenantId == _tenantContext.TenantId)
                .FirstOrDefaultAsync(s => s.FacebookPSID == psid);

            if (session == null)
            {
                _logger.LogWarning("Session not found for PSID {PSID}", psid);
                _awaitingFeedback.TryRemove(psid, out _);
                return;
            }

            // Find survey record
            var survey = await _dbContext.ConversationSurveys
                .FirstOrDefaultAsync(s => s.SessionId == session.Id);

            if (survey == null)
            {
                _logger.LogWarning("Survey not found for session {SessionId}", session.Id);
                _awaitingFeedback.TryRemove(psid, out _);
                return;
            }

            // Safe truncation that respects UTF-16 surrogate pairs and grapheme clusters
            if (feedbackText.Length > 500)
            {
                var stringInfo = new StringInfo(feedbackText);
                if (stringInfo.LengthInTextElements > 500)
                {
                    feedbackText = stringInfo.SubstringByTextElements(0, 500);
                }
            }

            // Update survey with feedback
            survey.FeedbackText = feedbackText;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Feedback saved for session {SessionId}", session.Id);

            // Send thank you message
            await _messengerService.SendTextMessageAsync(psid, _options.Messages.ThankYou);

            // Remove from awaiting feedback
            _awaitingFeedback.TryRemove(psid, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling feedback for PSID {PSID}", psid);
            _awaitingFeedback.TryRemove(psid, out _);
            throw;
        }
    }
}

public class CSATSurveyOptions
{
    public bool Enabled { get; set; } = true;
    public int DelayMinutes { get; set; } = 5;
    public bool SendFollowUpForLowRatings { get; set; } = true;
    public int LowRatingThreshold { get; set; } = 3;
    public CSATSurveyMessages Messages { get; set; } = new();
}

public class CSATSurveyMessages
{
    public string SurveyQuestion { get; set; } = "Bạn đánh giá trải nghiệm tư vấn như thế nào?";
    public string FollowUpQuestion { get; set; } = "Bạn có thể chia sẻ thêm để chúng em cải thiện không?";
    public string ThankYou { get; set; } = "Cảm ơn bạn đã đánh giá! Ý kiến của bạn giúp chúng em cải thiện dịch vụ.";
}
