namespace MessengerWebhook.Configuration;

public class LiveCommentOptions
{
    public const string SectionName = "LiveComment";

    public bool Enabled { get; set; } = true;
    public bool AutoHideComments { get; set; } = true;
    public List<string> TriggerKeywords { get; set; } = new();
    public string WelcomeMessage { get; set; } = string.Empty;
    public int MaxCommentsPerMinute { get; set; } = 50;
    public bool ProcessReplaysOnly { get; set; } = false;
}
