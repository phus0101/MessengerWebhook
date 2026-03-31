using System.Security.Claims;

namespace MessengerWebhook.Services.Admin;

public sealed record AdminAuthResult(
    bool Succeeded,
    string? Error,
    AdminUserContext? User,
    ClaimsPrincipal? Principal = null);
