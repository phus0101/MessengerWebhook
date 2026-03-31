namespace MessengerWebhook.Data.Entities;

public enum SupportCaseStatus
{
    Open = 0,
    Claimed = 1,
    Resolved = 2,
    Cancelled = 3
}

public enum SupportCaseReason
{
    PolicyException = 0,
    RefundRequest = 1,
    CancellationRequest = 2,
    PromptInjection = 3,
    UnsupportedQuestion = 4,
    ManualReview = 5
}

public class HumanSupportCase : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? CustomerIdentityId { get; set; }
    public Guid? DraftOrderId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public SupportCaseReason Reason { get; set; } = SupportCaseReason.ManualReview;
    public SupportCaseStatus Status { get; set; } = SupportCaseStatus.Open;
    public string Summary { get; set; } = string.Empty;
    public string TranscriptExcerpt { get; set; } = string.Empty;
    public string? AssignedToEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public bool ResumeBotOnNextMessage { get; set; } = true;
}
