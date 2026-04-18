using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.SmallTalk.Configuration;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Tone.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.SmallTalk;

/// <summary>
/// Service for detecting small talk and generating natural, context-aware responses
/// </summary>
public class SmallTalkService : ISmallTalkService
{
    private readonly SmallTalkDetector _detector;
    private readonly ILogger<SmallTalkService> _logger;
    private readonly SmallTalkOptions _options;

    public SmallTalkService(
        SmallTalkDetector detector,
        ILogger<SmallTalkService> logger,
        IOptions<SmallTalkOptions> options)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SmallTalkResponse> AnalyzeAsync(
        string message,
        EmotionScore emotion,
        ToneProfile toneProfile,
        ConversationContext conversationContext,
        VipProfile vipProfile,
        bool isReturningCustomer,
        int conversationTurnCount,
        CancellationToken cancellationToken = default)
    {
        var context = new SmallTalkContext
        {
            Message = message,
            Emotion = emotion,
            ToneProfile = toneProfile,
            ConversationContext = conversationContext,
            VipProfile = vipProfile,
            IsReturningCustomer = isReturningCustomer,
            ConversationTurnCount = conversationTurnCount,
            TimeOfDay = GetTimeOfDay()
        };

        return await AnalyzeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SmallTalkResponse> AnalyzeAsync(
        SmallTalkContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (!_options.EnableSmallTalkDetection)
        {
            return new SmallTalkResponse
            {
                Intent = SmallTalkIntent.None,
                IsSmallTalk = false,
                TransitionReadiness = TransitionReadiness.ReadyForBusiness,
                Confidence = 0.0
            };
        }

        // Detect intent
        var intent = _detector.DetectIntent(context.Message);
        var confidence = _detector.CalculateConfidence(context.Message, intent);

        _logger.LogDebug(
            "Small talk detection: intent={Intent}, confidence={Confidence:F2}, message='{Message}'",
            intent, confidence, context.Message);

        // Not small talk if confidence too low or business intent
        if (intent == SmallTalkIntent.None ||
            confidence < _options.SmallTalkConfidenceThreshold)
        {
            return new SmallTalkResponse
            {
                Intent = SmallTalkIntent.None,
                IsSmallTalk = false,
                TransitionReadiness = TransitionReadiness.ReadyForBusiness,
                Confidence = confidence
            };
        }

        // Determine transition readiness
        var transitionReadiness = DetermineTransitionReadiness(
            context.ConversationContext,
            context.ConversationTurnCount);

        // Generate suggested response
        var suggestedResponse = GenerateResponse(
            intent,
            context,
            transitionReadiness);

        var response = new SmallTalkResponse
        {
            Intent = intent,
            IsSmallTalk = true,
            SuggestedResponse = suggestedResponse,
            TransitionReadiness = transitionReadiness,
            Confidence = confidence,
            Metadata = new Dictionary<string, object>
            {
                ["timeOfDay"] = context.TimeOfDay.ToString(),
                ["isReturning"] = context.IsReturningCustomer,
                ["turnCount"] = context.ConversationTurnCount,
                ["vipTier"] = context.VipProfile.Tier.ToString()
            }
        };

        _logger.LogInformation(
            "Small talk detected: {Intent} (confidence: {Confidence:F2}), transition: {Transition}",
            response.Intent, response.Confidence, response.TransitionReadiness);

        return await Task.FromResult(response).ConfigureAwait(false);
    }

    private TransitionReadiness DetermineTransitionReadiness(
        ConversationContext conversationContext,
        int turnCount)
    {
        // Check for buying signals in context
        var hasBuyingSignal = conversationContext.Patterns
            .Any(p => p.Type == PatternType.BuyingSignal);

        if (hasBuyingSignal || conversationContext.CurrentStage == JourneyStage.Ready)
        {
            _logger.LogDebug("Buying signal detected, ready for business");
            return TransitionReadiness.ReadyForBusiness;
        }

        // After max small talk turns, offer help
        if (turnCount >= _options.MaxSmallTalkTurns && _options.EnableSoftTransitions)
        {
            _logger.LogDebug("Max small talk turns reached ({TurnCount}), offering help", turnCount);
            return TransitionReadiness.SoftOffer;
        }

        // Stay in small talk for first few turns
        return TransitionReadiness.StayInSmallTalk;
    }

    private string GenerateResponse(
        SmallTalkIntent intent,
        SmallTalkContext context,
        TransitionReadiness transitionReadiness)
    {
        var responses = new List<string>();

        // Generate base response based on intent
        switch (intent)
        {
            case SmallTalkIntent.Greeting:
                responses.Add(GenerateGreeting(context));
                break;

            case SmallTalkIntent.CheckIn:
                responses.Add("Dạ em đây ạ!");
                break;

            case SmallTalkIntent.Pleasantry:
                responses.Add("Dạ cảm ơn bạn! 😊");
                break;

            case SmallTalkIntent.Acknowledgment:
                responses.Add("Dạ vâng ạ!");
                break;
        }

        // Greeting should always include a transition question so the customer is not left hanging.
        if (intent == SmallTalkIntent.Greeting)
        {
            if (transitionReadiness == TransitionReadiness.SoftOffer)
            {
                responses.Add("Có gì em giúp được không ạ?");
            }
            else if (transitionReadiness == TransitionReadiness.ReadyForBusiness)
            {
                responses.Add("Em có thể tư vấn sản phẩm cho bạn nha!");
            }
            else
            {
                responses.Add(context.IsReturningCustomer || context.VipProfile.Tier == VipTier.Vip
                    ? "Hôm nay chị đang cần em hỗ trợ gì ạ?"
                    : "Hôm nay bạn đang cần em hỗ trợ gì ạ?");
            }
        }
        else if (transitionReadiness == TransitionReadiness.SoftOffer)
        {
            responses.Add("Có gì em giúp được không ạ?");
        }
        else if (transitionReadiness == TransitionReadiness.ReadyForBusiness)
        {
            responses.Add("Em có thể tư vấn sản phẩm cho bạn nha!");
        }

        return string.Join(" ", responses);
    }

    private string GenerateGreeting(SmallTalkContext context)
    {
        if (!_options.EnableContextAwareGreetings)
            return "Chào bạn! 😊";

        // Time-aware greeting
        var timeGreeting = context.TimeOfDay switch
        {
            TimeOfDay.Morning => "Chào buổi sáng",
            TimeOfDay.Afternoon => "Chào buổi chiều",
            TimeOfDay.Evening => "Chào buổi tối",
            _ => "Chào"
        };

        // VIP personalization
        if (context.VipProfile.Tier == VipTier.Vip)
        {
            _logger.LogDebug("Generating VIP greeting");
            return $"{timeGreeting} chị! Em rất vui được phục vụ chị ạ! 😊";
        }

        // Returning customer personalization
        if (context.IsReturningCustomer)
        {
            _logger.LogDebug("Generating returning customer greeting");
            return $"{timeGreeting} bạn! Vui được gặp lại bạn nha! 😊";
        }

        // Casual tone for excited customers
        if (context.Emotion.PrimaryEmotion == EmotionType.Excited)
        {
            _logger.LogDebug("Generating excited greeting");
            return "Alo bạn! 😊";
        }

        // Default friendly greeting
        return $"{timeGreeting} bạn! 😊";
    }

    private static TimeOfDay GetTimeOfDay()
    {
        // Use UTC+7 (Vietnam timezone) for consistent greeting logic
        var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
        );
        var hour = vietnamTime.Hour;
        if (hour >= 5 && hour < 12) return TimeOfDay.Morning;
        if (hour >= 12 && hour < 18) return TimeOfDay.Afternoon;
        return TimeOfDay.Evening;
    }
}
