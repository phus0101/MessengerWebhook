namespace MessengerWebhook.Configuration;

public sealed class PolicyGuardOptions
{
    public const string SectionName = "PolicyGuard";

    public string[] EscalationKeywords { get; set; } = [];
    public string SafeReplyMessage { get; set; } =
        "Chị iu ơi, phần này em chưa thể xác nhận ngay trong chat. Nếu chị muốn, em sẽ chuyển chị qua bạn hỗ trợ của Mũi Xù để kiểm tra kỹ hơn nha.";
    public bool EnableRegexDetector { get; set; } = true;
    public bool EnableFuzzyDetector { get; set; } = true;
    public bool EnableSemanticClassifier { get; set; } = false;
    public decimal SemanticClassifierMinConfidence { get; set; } = 0.85m;
    public decimal SafeReplyThreshold { get; set; } = 0.35m;
    public decimal SoftEscalateThreshold { get; set; } = 0.60m;
    public decimal HardEscalateThreshold { get; set; } = 0.80m;
    public decimal RepeatMentionBoost { get; set; } = 0.10m;
    public decimal OpenSupportCaseBoost { get; set; } = 0.15m;
    public decimal DraftOrderBoost { get; set; } = 0.05m;
    public int MaxRecentTurns { get; set; } = 5;
    public int ClassifierTimeoutMs { get; set; } = 1200;
}
