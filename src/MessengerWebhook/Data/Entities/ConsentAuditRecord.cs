namespace MessengerWebhook.Data.Entities;

public class ConsentAuditRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CustomerPsid { get; set; } = "";
    public ConsentDecision Decision { get; set; }
    public string Purpose { get; set; } = "";
    public string Channel { get; set; } = "messenger";
    public string ConsentTextShown { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? WithdrawnReason { get; set; }
}

public enum ConsentDecision { Given, Refused, Implied, Withdrawn }
