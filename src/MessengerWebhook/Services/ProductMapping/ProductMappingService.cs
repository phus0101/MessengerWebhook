using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;

namespace MessengerWebhook.Services.ProductMapping;

/// <summary>
/// Maps quick reply payloads and customer language into product records.
/// </summary>
public class ProductMappingService : IProductMappingService
{
    private readonly IProductRepository _productRepository;
    private static readonly Regex ProductCodeRegex = new(@"^[A-Z0-9_]+$", RegexOptions.Compiled);

    public ProductMappingService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product?> GetProductByPayloadAsync(string payload)
    {
        if (!IsValidPayload(payload))
        {
            return null;
        }

        var code = payload.Replace("PRODUCT_", string.Empty, StringComparison.OrdinalIgnoreCase);
        return IsValidProductCode(code) ? await GetProductByCodeAsync(code) : null;
    }

    public Task<Product?> GetProductByCodeAsync(string code)
    {
        return _productRepository.GetByCodeAsync(code);
    }

    public async Task<Product?> GetProductByMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = Normalize(message);

        // Kem Trị Nám Tàn Nhang
        if (ContainsAny(normalized, "tri nam", "tan nhang", "nam", "ktn", "tri tham", "lam mo tham"))
        {
            return await GetProductByCodeAsync("KTN");
        }

        // Kem Chống Nắng
        if (ContainsAny(normalized, "kem chong nang", "chong nang", "kcn", "spf"))
        {
            return await GetProductByCodeAsync("KCN");
        }

        // Kem Lụa Dưỡng Ẩm
        if (ContainsAny(normalized, "kem lua", "lua", "duong am", "cap am"))
        {
            return await GetProductByCodeAsync("KL");
        }

        // Sữa Rửa Mặt
        if (ContainsAny(normalized, "sua rua mat", "rua mat", "srm", "lam sach"))
        {
            return await GetProductByCodeAsync("SRM");
        }

        // Toner
        if (ContainsAny(normalized, "toner", "nuoc hoa hong", "can bang da"))
        {
            return await GetProductByCodeAsync("TN");
        }

        // Serum Vitamin C
        if (ContainsAny(normalized, "serum", "vitamin c", "lam sang", "chong lao hoa"))
        {
            return await GetProductByCodeAsync("SR");
        }

        // Mặt Nạ Ngủ
        if (ContainsAny(normalized, "mat na", "mat na ngu", "mask"))
        {
            return await GetProductByCodeAsync("MN");
        }

        // Kem Dưỡng Mắt
        if (ContainsAny(normalized, "kem mat", "duong mat", "quang tham", "bong mat"))
        {
            return await GetProductByCodeAsync("KDM");
        }

        // Combo 3 - Trị Nám Toàn Diện
        if (ContainsAny(normalized, "combo 3", "combo tri nam", "combo toan dien"))
        {
            return await GetProductByCodeAsync("COMBO_3");
        }

        // Combo 2 - Freeship
        if (ContainsAny(normalized, "freeship", "2 san pham", "combo 2", "combo"))
        {
            return await GetProductByCodeAsync("COMBO_2");
        }

        return null;
    }

    public bool IsValidPayload(string payload)
    {
        return !string.IsNullOrWhiteSpace(payload) &&
               payload.StartsWith("PRODUCT_", StringComparison.OrdinalIgnoreCase) &&
               payload.Length > "PRODUCT_".Length;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static bool IsValidProductCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) && ProductCodeRegex.IsMatch(code);
    }

    private static string Normalize(string input)
    {
        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var buffer = new List<char>(decomposed.Length);

        foreach (var character in decomposed)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer.Add(character switch
            {
                'đ' => 'd',
                'Đ' => 'd',
                _ => character
            });
        }

        return new string(buffer.ToArray()).Normalize(NormalizationForm.FormC);
    }
}
