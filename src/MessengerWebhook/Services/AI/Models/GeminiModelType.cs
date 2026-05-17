namespace MessengerWebhook.Services.AI.Models;

public enum GeminiModelType
{
    /// <summary>Lowest-cost tier for classify/summarize tasks.</summary>
    FlashLite,
    /// <summary>Mid-tier for standard chat interactions.</summary>
    Flash,
    /// <summary>Highest-capability tier for complex/VIP/low-confidence scenarios.</summary>
    Pro
}
