using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;

namespace MessengerWebhook.Services.GiftSelection;

/// <summary>
/// Service for selecting gifts based on product
/// </summary>
public class GiftSelectionService : IGiftSelectionService
{
    private readonly IProductGiftMappingRepository _mappingRepository;

    public GiftSelectionService(IProductGiftMappingRepository mappingRepository)
    {
        _mappingRepository = mappingRepository;
    }

    public async Task<Gift?> SelectGiftForProductAsync(string productCode)
    {
        var mappings = await _mappingRepository.GetByProductCodeAsync(productCode);

        // Return first active gift (already ordered by priority)
        return mappings.FirstOrDefault()?.Gift;
    }

    public async Task<List<Gift>> GetAvailableGiftsForProductAsync(string productCode)
    {
        var mappings = await _mappingRepository.GetByProductCodeAsync(productCode);

        return mappings
            .Where(m => m.Gift != null)
            .Select(m => m.Gift!)
            .ToList();
    }
}
