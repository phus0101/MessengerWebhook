namespace MessengerWebhook.Data.Entities;

public interface ITenantOwnedEntity
{
    Guid? TenantId { get; set; }
}
