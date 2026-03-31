namespace MessengerWebhook.Services.AI.Models;

/// <summary>
/// Represents the detected intent of a customer message in the sales conversation flow.
/// Used by AI-based intent detection to route conversations appropriately.
/// </summary>
public enum CustomerIntent
{
    /// <summary>
    /// Customer is exploring products, not ready to buy yet.
    /// Examples: "tính xem", "xem thử", "có sản phẩm gì"
    /// </summary>
    Browsing,

    /// <summary>
    /// Customer needs advice or consultation before making a purchase decision.
    /// Examples: "cần tư vấn", "hỏi thêm", "cho em hỏi"
    /// </summary>
    Consulting,

    /// <summary>
    /// Customer is ready to place an order.
    /// Examples: "đặt hàng", "chốt đơn", "lên đơn luôn", "mua luôn"
    /// </summary>
    ReadyToBuy,

    /// <summary>
    /// Customer is confirming previously provided information.
    /// Examples: "đúng rồi", "ok em", "vâng ạ"
    /// </summary>
    Confirming,

    /// <summary>
    /// Customer is asking questions about products, shipping, or policies.
    /// Examples: "ship bao lâu?", "giá bao nhiêu?", "có freeship không?"
    /// </summary>
    Questioning
}
