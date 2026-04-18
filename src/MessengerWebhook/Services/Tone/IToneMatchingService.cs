using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tone.Models;

namespace MessengerWebhook.Services.Tone;

/// <summary>
/// Service for matching bot tone to customer emotion and context
/// </summary>
public interface IToneMatchingService
{
    /// <summary>
    /// Generate tone profile from aggregated context
    /// </summary>
    Task<ToneProfile> GenerateToneProfileAsync(
        ToneContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate tone profile from individual components
    /// </summary>
    Task<ToneProfile> GenerateToneProfileAsync(
        EmotionScore emotion,
        VipProfile vipProfile,
        CustomerIdentity customer,
        int conversationTurnCount = 0,
        CancellationToken cancellationToken = default);
}
