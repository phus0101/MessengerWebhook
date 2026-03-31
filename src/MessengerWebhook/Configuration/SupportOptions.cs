namespace MessengerWebhook.Configuration;

public class SupportOptions
{
    public const string SectionName = "Support";

    public string DefaultManagerEmail { get; set; } = string.Empty;
    public int BotLockTimeoutMinutes { get; set; } = 120;
    public bool ResumeBotOnNextMessage { get; set; } = true;
    public int TokenExpirationDays { get; set; } = 7;
    public string TokenSecret { get; set; } = string.Empty;
}
