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
    DraftOrderStatus Status,
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
    string? CustomerName,
    string CustomerPhone,
    string ShippingAddress,
    DraftOrderStatus Status,
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
    IReadOnlyList<AdminDraftOrderItemDto> Items,
    IReadOnlyList<AdminAuditLogDto> AuditLogs);

public sealed record AdminSupportCaseListItemDto(
    Guid Id,
    string FacebookPSID,
    string? FacebookPageId,
    SupportCaseReason Reason,
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
    SupportCaseReason Reason,
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
