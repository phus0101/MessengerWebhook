namespace MessengerWebhook.Services.Policy;

public sealed record PolicyGuardRequest(
    string Message,
    bool HasOpenSupportCase = false,
    bool HasDraftOrder = false,
    IReadOnlyList<PolicyConversationTurn>? RecentTurns = null,
    string? FacebookPSID = null,
    string? FacebookPageId = null,
    string? CurrentState = null,
    string? KnownIntent = null,
    IReadOnlyList<string>? SelectedProductCodes = null);
