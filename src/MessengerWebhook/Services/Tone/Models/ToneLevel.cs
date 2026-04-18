namespace MessengerWebhook.Services.Tone.Models;

/// <summary>
/// Represents the formality level of bot responses
/// </summary>
public enum ToneLevel
{
    /// <summary>
    /// Formal, respectful tone (VIP customers, new customers)
    /// Vietnamese: Trang trọng, lịch sự
    /// </summary>
    Formal,

    /// <summary>
    /// Friendly, approachable tone (returning customers)
    /// Vietnamese: Thân thiện, gần gũi
    /// </summary>
    Friendly,

    /// <summary>
    /// Casual, relaxed tone (excited customers, close relationship)
    /// Vietnamese: Thoải mái, vui vẻ
    /// </summary>
    Casual
}
