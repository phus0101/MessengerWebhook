namespace MessengerWebhook.Data.Entities;

public enum DraftOrderStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    SubmittedToNobita = 4,
    SubmitFailed = 5
}

public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public class DraftOrder : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string DraftCode { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public Guid? CustomerIdentityId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public string? FacebookPageId { get; set; }
    public string? CustomerName { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public decimal MerchandiseTotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal GrandTotal { get; set; }
    public DraftOrderStatus Status { get; set; } = DraftOrderStatus.PendingReview;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public bool RequiresManualReview { get; set; } = true;
    public string? RiskSummary { get; set; }
    public string? CustomerNotes { get; set; }
    public string? AssignedManagerEmail { get; set; }
    public string? NobitaOrderId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByEmail { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedByEmail { get; set; }
    public int SubmissionAttemptCount { get; set; }
    public DateTime? LastSubmissionAttemptAt { get; set; }
    public string? LastSubmissionError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public CustomerIdentity? CustomerIdentity { get; set; }
    public ICollection<DraftOrderItem> Items { get; set; } = new List<DraftOrderItem>();
    public ICollection<RiskSignal> RiskSignals { get; set; } = new List<RiskSignal>();
}
