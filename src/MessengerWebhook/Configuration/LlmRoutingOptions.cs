namespace MessengerWebhook.Configuration;

public class LlmRoutingOptions
{
    public const string SectionName = "LlmRouting";

    /// <summary>When false, routing logic is bypassed (Flash for chat, FlashLite for classify/summarize).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Intent confidence below this threshold triggers Pro tier.</summary>
    public float LowConfidenceThreshold { get; set; } = 0.6f;

    /// <summary>Conversation turns above this count triggers Pro tier.</summary>
    public int LongConversationThreshold { get; set; } = 8;

    /// <summary>Estimated order value (VND) at or above this triggers Pro tier.</summary>
    public decimal ProTierMinTicketValueVnd { get; set; } = 1_000_000;
}
