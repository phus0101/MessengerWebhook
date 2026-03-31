namespace MessengerWebhook.Configuration;

public class SalesBotOptions
{
    public const string SectionName = "SalesBot";

    public string ClosingCallToAction { get; set; } =
        "Chi iu cho em xin so dien thoai va dia chi em len don luon nha.";

    public string EscalationKeywords { get; set; } =
        "huy don,hoan tien,refund,prompt injection,mien phi van chuyen,them khuyen mai,giam gia them,nhan vien ho tro";

    public int VipOrderThreshold { get; set; } = 3;
    public decimal HighRiskThreshold { get; set; } = 0.50m;
    public string UnsupportedFallbackMessage { get; set; } =
        "Da em xin phep chuyen chi qua ban ho tro cua Mui Xu de xu ly ky hon nha.";
}
