using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Tone.Models;

namespace MessengerWebhook.Services.ResponseValidation.Models;

/// <summary>
/// Context for validating a bot response
/// </summary>
public class ResponseValidationContext
{
    public string Response { get; set; } = string.Empty;
    public ToneProfile ToneProfile { get; set; } = null!;
    public ConversationContext ConversationContext { get; set; } = null!;
    public SmallTalkResponse? SmallTalkResponse { get; set; }
    public bool RequiresFactGrounding { get; set; }
    public bool ResponseContainsProductMention { get; set; }
    public List<string> AllowedProductNames { get; set; } = new();
    public List<string> AllowedProductCodes { get; set; } = new();
    public List<decimal> AllowedPrices { get; set; } = new();
    public bool AllowPolicyFacts { get; set; }
    public bool AllowInventoryFacts { get; set; }
    public bool AllowOrderFacts { get; set; }
}
