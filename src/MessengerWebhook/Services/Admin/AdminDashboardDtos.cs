using System.Text.Json.Serialization;
using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Admin;

public sealed record AdminOverviewDto(
    int PendingDrafts,
    int SubmitFailedDrafts,
    int OpenSupportCases,
    int ClaimedSupportCases);

public sealed record AdminDraftOrderListItemDto(
    Guid Id,
    string DraftCode,
    string? FacebookPageId,
    string? CustomerName,
    string CustomerPhone,
    string ShippingAddress,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    DraftOrderStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RiskLevel RiskLevel,
    bool RequiresManualReview,
    string? AssignedManagerEmail,
    int ItemCount,
    decimal GrandTotal,
    DateTime CreatedAt);

public sealed record AdminDraftOrderItemDto(
    Guid Id,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string? GiftCode,
    string? GiftName);

public sealed record AdminGiftOptionDto(
    string Code,
    string Name);

public sealed record AdminDraftProductOptionDto(
    string Code,
    string Name,
    decimal UnitPrice,
    IReadOnlyList<AdminGiftOptionDto> GiftOptions);

public sealed record AdminAuditLogDto(
    Guid Id,
    string ActorEmail,
    string Action,
    string ResourceType,
    string ResourceId,
    string? Details,
    DateTime CreatedAt);

public sealed record AdminDraftOrderDetailDto(
    Guid Id,
    string DraftCode,
    string? FacebookPageId,
    Guid? CustomerIdentityId,
    string? CustomerName,
    string CustomerPhone,
    string ShippingAddress,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    DraftOrderStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RiskLevel RiskLevel,
    string? RiskSummary,
    bool RequiresManualReview,
    decimal MerchandiseTotal,
    decimal ShippingFee,
    decimal GrandTotal,
    string? AssignedManagerEmail,
    string? NobitaOrderId,
    string? LastSubmissionError,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByEmail,
    DateTime? SubmittedAt,
    string? SubmittedByEmail,
    bool IsEditable,
    AdminCustomerOptionDto? LinkedCustomer,
    IReadOnlyList<AdminDraftOrderItemDto> Items,
    IReadOnlyList<AdminDraftProductOptionDto> AvailableProducts,
    IReadOnlyList<AdminAuditLogDto> AuditLogs);

public sealed record AdminCustomerOptionDto(
    Guid CustomerIdentityId,
    string? FullName,
    string? PhoneNumber,
    string? ShippingAddress,
    int TotalOrders,
    int SuccessfulDeliveries,
    int FailedDeliveries,
    DateTime? LastInteractionAt);

public sealed record AdminSupportCaseListItemDto(
    Guid Id,
    string FacebookPSID,
    string? FacebookPageId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SupportCaseReason Reason,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SupportCaseStatus Status,
    string Summary,
    string? AssignedToEmail,
    DateTime CreatedAt,
    DateTime? ClaimedAt,
    DateTime? ResolvedAt);

public sealed record AdminSupportCaseDetailDto(
    Guid Id,
    string FacebookPSID,
    string? FacebookPageId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SupportCaseReason Reason,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SupportCaseStatus Status,
    string Summary,
    string TranscriptExcerpt,
    string? AssignedToEmail,
    string? ClaimedByEmail,
    string? ResolvedByEmail,
    string? ResolutionNotes,
    DateTime CreatedAt,
    DateTime? ClaimedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<AdminAuditLogDto> AuditLogs);

public sealed record AdminProductMappingDto(
    string Id,
    string Code,
    string Name,
    decimal BasePrice,
    int? NobitaProductId,
    decimal NobitaWeight,
    DateTime? NobitaLastSyncedAt,
    string? NobitaSyncError);

public sealed record NobitaProductOptionDto(
    int ProductId,
    string Code,
    string Name,
    decimal Price,
    bool IsOutOfStock);

public sealed record AdminCommandResult(
    bool Succeeded,
    string Message,
    string? ExternalReference = null);
