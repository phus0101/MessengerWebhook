namespace MessengerWebhook.Configuration;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; init; } = string.Empty;
    public string ChatId { get; init; } = string.Empty;
    public bool Enabled { get; init; } = false;
}
