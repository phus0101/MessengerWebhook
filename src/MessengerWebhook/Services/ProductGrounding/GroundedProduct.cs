namespace MessengerWebhook.Services.ProductGrounding;

public record GroundedProduct(
    string Id,
    string Code,
    string Name,
    string? Category,
    decimal? Price);
