namespace MessengerWebhook.Services.ProductGrounding;

public interface IProductNeedDetector
{
    bool RequiresProductGrounding(string message);
}

public class ProductNeedDetector : IProductNeedDetector
{
    private static readonly string[] ProductTerms =
    {
        "sản phẩm", "san pham", "mỹ phẩm", "my pham", "mặt nạ", "mat na", "kem", "serum", "toner",
        "sữa rửa mặt", "sua rua mat", "chống nắng", "chong nang", "tẩy trang", "tay trang"
    };

    private static readonly string[] NeedTerms =
    {
        "dưỡng ẩm", "duong am", "cấp ẩm", "cap am", "khô", "kho", "thiếu ẩm", "thieu am",
        "nám", "nam", "tàn nhang", "tan nhang", "đốm nâu", "dom nau", "sạm", "sam",
        "mụn", "mun", "dầu", "dau", "phục hồi", "phuc hoi", "sáng da", "sang da"
    };

    private static readonly string[] CatalogIntentTerms =
    {
        "catalog", "danh sách", "danh sach", "sản phẩm nào", "san pham nao", "các loại", "cac loai",
        "có loại nào", "co loai nao", "có những loại", "co nhung loai", "loại nào", "loai nao",
        "dòng nào", "dong nao", "mẫu nào", "mau nao", "bên em có", "ben em co", "shop có", "shop co"
    };

    private static readonly string[] FactIntentTerms =
    {
        "giá", "gia", "công dụng", "cong dung", "tác dụng", "tac dung", "thành phần", "thanh phan",
        "cách dùng", "cach dung", "còn hàng", "con hang", "hết hàng", "het hang", "tồn kho", "ton kho"
    };

    public bool RequiresProductGrounding(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var hasProductTerm = ContainsAny(message, ProductTerms);
        if (!hasProductTerm)
        {
            return false;
        }

        return ContainsAny(message, NeedTerms) ||
               ContainsAny(message, CatalogIntentTerms) ||
               ContainsAny(message, FactIntentTerms) ||
               message.Contains('?');
    }

    private static bool ContainsAny(string value, IEnumerable<string> phrases)
    {
        return phrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }
}
