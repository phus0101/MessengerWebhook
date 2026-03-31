namespace MessengerWebhook.Configuration;

public class LiveCommentOptions
{
    public const string SectionName = "LiveComment";

    public bool Enabled { get; set; } = true;
    public bool AutoHideComments { get; set; } = true;
    public List<string> TriggerKeywords { get; set; } = new();
    public string WelcomeMessage { get; set; } = string.Empty;

    // Multi-layer rate limiting
    public int MaxCommentsPerMinute { get; set; } = 50; // Global rate limit
    public int MaxRepliesPerVideo { get; set; } = 100; // Per-video limit
    public int MaxRepliesPerUser { get; set; } = 3; // Per-user limit
    public int GlobalMaxRepliesPerMinute { get; set; } = 50; // Global replies per minute

    // Public reply options
    public bool EnablePublicReply { get; set; } = false;
    public string PublicReplyTemplate { get; set; } = "Cảm ơn bạn đã quan tâm! Mình đã nhắn tin riêng cho bạn rồi nha 💬";

    public bool ProcessReplaysOnly { get; set; } = false;
}
