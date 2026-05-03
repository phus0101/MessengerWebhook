using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public sealed record CommercialFactSnapshot(
    bool PriceConfirmed,
    decimal? ConfirmedPrice,
    bool InventoryConfirmed,
    bool? IsInStock,
    int? StockQuantity,
    string? PriceLabel,
    bool GiftConfirmed,
    string? GiftName,
    decimal? ShippingFee,
    bool ShippingConfirmed,
    bool? IsFreeship)
{
    public static CommercialFactSnapshot Create(
        Product product,
        ProductVariant? selectedVariant,
        Gift? gift,
        decimal? shippingFee,
        bool shippingConfirmed)
    {
        var productRequiresVariant = product.Variants.Count > 0;
        decimal? confirmedPrice = selectedVariant?.Price > 0m
            ? selectedVariant.Price
            : !productRequiresVariant && product.BasePrice > 0m
                ? product.BasePrice
                : null;
        var inventoryConfirmed = selectedVariant != null;
        var shippingFeeConfirmed = shippingConfirmed && shippingFee.HasValue;

        return new CommercialFactSnapshot(
            confirmedPrice.HasValue,
            confirmedPrice,
            inventoryConfirmed,
            inventoryConfirmed ? selectedVariant!.IsAvailable && selectedVariant.StockQuantity > 0 : null,
            inventoryConfirmed ? selectedVariant!.StockQuantity : null,
            selectedVariant == null ? null : BuildPriceLabel(selectedVariant),
            gift != null && !string.IsNullOrWhiteSpace(gift.Name),
            gift?.Name,
            shippingFeeConfirmed ? shippingFee : null,
            shippingFeeConfirmed,
            shippingFeeConfirmed ? shippingFee == 0m : null);
    }

    private static string? BuildPriceLabel(ProductVariant variant)
    {
        var parts = new List<string>();
        if (variant.VolumeML > 0)
        {
            parts.Add($"{variant.VolumeML}ml");
        }

        if (!string.IsNullOrWhiteSpace(variant.Texture))
        {
            parts.Add(variant.Texture);
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}
