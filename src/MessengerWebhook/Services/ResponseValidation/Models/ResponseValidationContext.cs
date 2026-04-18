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
}
