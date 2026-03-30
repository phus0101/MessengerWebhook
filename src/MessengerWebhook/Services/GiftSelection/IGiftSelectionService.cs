using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.GiftSelection;

/// <summary>
/// Service for selecting gifts based on product
/// </summary>
public interface IGiftSelectionService
{
    /// <summary>
    /// Select gift for product based on priority
    /// Returns highest priority active gift
    /// </summary>
    Task<Gift?> SelectGiftForProductAsync(string productCode);

    /// <summary>
    /// Get all available gifts for product
    /// </summary>
    Task<List<Gift>> GetAvailableGiftsForProductAsync(string productCode);
}
