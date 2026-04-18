namespace MessengerWebhook.Services.Metrics.Models;

public class ConversationTrendDto
{
    public string Date { get; set; } = string.Empty;
    public double CompletionRate { get; set; }
    public double EscalationRate { get; set; }
    public double AbandonmentRate { get; set; }
    public double AvgMessages { get; set; }
}
