namespace MessengerWebhook.Data.Entities;

public class DraftOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraftOrderId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? GiftCode { get; set; }
    public string? GiftName { get; set; }

    public DraftOrder? DraftOrder { get; set; }
}
