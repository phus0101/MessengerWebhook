using MessengerWebhook.Services.Notifications;

namespace MessengerWebhook.Endpoints;

public static class AlertWebhookEndpointExtensions
{
    public static IEndpointRouteBuilder MapAlertWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Seq alert → this endpoint → Telegram
        // Protect with shared secret in X-Internal-Api-Key header
        endpoints.MapPost("/internal/alerts/seq", async (
            HttpContext ctx,
            SeqAlertPayload? payload,
            TelegramNotifier notifier,
            AlertDeduplicator dedup,
            IConfiguration config,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var expectedKey = config["Alerts:InternalApiKey"];
            if (!string.IsNullOrWhiteSpace(expectedKey))
            {
                var providedKey = ctx.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
                if (providedKey != expectedKey)
                {
                    logger.LogWarning("Rejected alert webhook: invalid API key from {RemoteIp}",
                        ctx.Connection.RemoteIpAddress);
                    return Results.Unauthorized();
                }
            }

            var alertType = payload?.Title ?? "unknown";

            if (!dedup.TryAcquire(alertType))
            {
                logger.LogDebug("Alert deduplicated: {AlertType}", alertType);
                return Results.Ok(new { status = "deduplicated" });
            }

            var message = FormatTelegramMessage(payload);
            await notifier.SendAsync(message, cancellationToken);

            logger.LogInformation("AlertDispatched AlertType={AlertType}", alertType);
            return Results.Ok(new { status = "sent" });
        });

        return endpoints;
    }

    private static string FormatTelegramMessage(SeqAlertPayload? payload)
    {
        if (payload is null) return "[P1] Unknown alert triggered";

        var severity = payload.Title?.Contains("dropped", StringComparison.OrdinalIgnoreCase) == true
            ? "P1 🔴"
            : "P1 🚨";

        var lines = new List<string>
        {
            $"<b>{severity} {payload.Title ?? "Alert"}</b>",
            payload.Message ?? string.Empty,
            $"<i>{payload.Timestamp:HH:mm 'ICT' dd/MM}</i>"
        };

        if (payload.ResultRows?.Count > 0)
        {
            var row = payload.ResultRows[0];
            lines.Add($"Data: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }

        return string.Join("\n", lines.Where(l => l.Length > 0));
    }

    // Flexible model matching Seq webhook payload (Seq 2024+)
    internal sealed record SeqAlertPayload(
        string? Id,
        string? Title,
        string? Message,
        DateTimeOffset Timestamp,
        string? OwnerUsername,
        List<Dictionary<string, object?>>? ResultRows
    );
}
