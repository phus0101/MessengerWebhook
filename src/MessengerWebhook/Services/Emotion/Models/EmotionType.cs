namespace MessengerWebhook.Services.Emotion.Models;

/// <summary>
/// Represents the primary emotion types detected in customer messages
/// </summary>
public enum EmotionType
{
    /// <summary>
    /// Positive emotion - customer is happy, satisfied, pleased
    /// Examples: "tuyệt vời", "hay quá", "ok", "được"
    /// </summary>
    Positive,

    /// <summary>
    /// Neutral emotion - customer is calm, matter-of-fact
    /// Examples: "oke", "được", "vâng"
    /// </summary>
    Neutral,

    /// <summary>
    /// Negative emotion - customer is unhappy, dissatisfied
    /// Examples: "không tốt", "dở", "tệ"
    /// </summary>
    Negative,

    /// <summary>
    /// Frustrated emotion - customer is annoyed, angry, impatient
    /// Examples: "bực", "tức", "chán", "mệt mỏi"
    /// </summary>
    Frustrated,

    /// <summary>
    /// Excited emotion - customer is enthusiastic, eager
    /// Examples: "wow", "quá đỉnh", "tuyệt vời quá"
    /// </summary>
    Excited
}
