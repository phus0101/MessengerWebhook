using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public interface IRiskMessageSanitizer
{
    string SanitizeForCustomer(RiskLevel level);
}

public class RiskMessageSanitizer : IRiskMessageSanitizer
{
    public string SanitizeForCustomer(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.High => "Đơn hàng cần xác nhận thêm thông tin",
            RiskLevel.Medium => "Đơn hàng đang được xử lý",
            RiskLevel.Low => "Đơn hàng hợp lệ",
            _ => "Đơn hàng đang được xử lý"
        };
    }
}
