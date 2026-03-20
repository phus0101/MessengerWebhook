namespace MessengerWebhook.Services.AI.Models;

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "model"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
