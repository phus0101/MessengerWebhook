namespace MessengerWebhook.Services.ProductGrounding;

public record GroundedProductContext(
    bool RequiresGrounding,
    IReadOnlyList<GroundedProduct> AllowedProducts,
    string FallbackReply)
{
    public bool HasAllowedProducts => AllowedProducts.Count > 0;
}
