namespace MessengerWebhook.Services.SmallTalk.Models;

/// <summary>
/// Represents the intent classification for small talk messages
/// </summary>
public enum SmallTalkIntent
{
    /// <summary>
    /// Not small talk, business intent detected
    /// </summary>
    None,

    /// <summary>
    /// Casual greeting: "hi", "hello", "chào"
    /// </summary>
    Greeting,

    /// <summary>
    /// Check-in message: "có ai không", "shop ơi"
    /// </summary>
    CheckIn,

    /// <summary>
    /// Social pleasantry: "trời đẹp", "buổi sáng vui vẻ"
    /// </summary>
    Pleasantry,

    /// <summary>
    /// Short acknowledgment: "ok", "oke", "được"
    /// </summary>
    Acknowledgment
}
