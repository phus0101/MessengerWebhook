using MessengerWebhook.Models;

namespace MessengerWebhook.Services.AI.Resilience;

/// <summary>
/// Returns pre-canned degraded responses when the LLM circuit breaker is open.
/// </summary>
public class LlmFallbackService : ILlmFallbackService
{
    public bool IsCircuitOpen { get; set; }

    public string GetDegradedResponse(ConversationState state) => state switch
    {
        ConversationState.Consulting or ConversationState.DraftOrder
            => "Dạ em đang bận một chút, chị nhắn lại sau vài phút nha ạ.",
        ConversationState.CollectingInfo
            => "Dạ em ghi nhận thông tin rồi ạ, chị chờ em xử lý một chút.",
        _ => "Dạ hệ thống đang bận, chị vui lòng nhắn lại sau ít phút ạ."
    };
}
