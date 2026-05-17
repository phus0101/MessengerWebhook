namespace MessengerWebhook.Configuration;

public class ConsentOptions
{
    public const string SectionName = "Consent";

    public string DefaultConsentText { get; set; } =
        "Bên em chỉ dùng thông tin này để giao đơn và liên hệ về đơn hiện tại. Không chia sẻ với bên thứ 3 ngoài đơn vị vận chuyển. Chị đồng ý chứ ạ?";

    public string PrivacyPolicyUrl { get; set; } = "";

    public int RetentionDays { get; set; } = 30;
}
