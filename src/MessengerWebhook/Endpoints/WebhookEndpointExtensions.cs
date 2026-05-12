using System.Threading.Channels;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services.LiveComments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Endpoints;

internal static class WebhookEndpointExtensions
{
    internal static void MapWebhookEndpoints(this WebApplication app)
    {
        app.MapGet("/webhook", (
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            IOptions<WebhookOptions> options,
            ILogger<Program> logger) =>
        {
            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(verifyToken) || string.IsNullOrEmpty(challenge))
            {
                logger.LogWarning("Webhook verification failed: Missing parameters");
                return Results.BadRequest("Missing required parameters");
            }
            if (mode != "subscribe")
            {
                logger.LogWarning("Webhook verification failed: Invalid mode {Mode}", mode);
                return Results.StatusCode(403);
            }
            if (verifyToken != options.Value.VerifyToken)
            {
                logger.LogWarning("Webhook verification failed: Invalid verify token");
                return Results.StatusCode(403);
            }
            logger.LogInformation("Webhook verified successfully");
            return Results.Text(challenge);
        })
        .WithName("VerifyWebhook");

        app.MapPost("/webhook", async (
            WebhookEvent webhookEvent,
            Channel<MessagingEvent> channel,
            [FromKeyedServices("liveComment")] SemaphoreSlim liveCommentSemaphore,
            IServiceScopeFactory scopeFactory,
            ILogger<Program> logger) =>
        {
            if (webhookEvent.Object != "page")
            {
                logger.LogWarning("Invalid object type: {Object}", webhookEvent.Object);
                return Results.NotFound();
            }

            var eventCount = 0;
            foreach (var entry in webhookEvent.Entry)
            {
                if (entry.Messaging != null)
                {
                    foreach (var messagingEvent in entry.Messaging)
                    {
                        await channel.Writer.WriteAsync(messagingEvent);
                        eventCount++;
                    }
                }

                if (entry.Changes != null)
                {
                    foreach (var change in entry.Changes)
                    {
                        if (change.Field == "live_comments" && change.Value?.CommentId != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                using var scope = scopeFactory.CreateScope();
                                var liveCommentService = scope.ServiceProvider.GetRequiredService<ILiveCommentAutomationService>();
                                var commentLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                                await liveCommentSemaphore.WaitAsync();
                                try
                                {
                                    var shouldHandle = await liveCommentService.ShouldHandleCommentAsync(
                                        change.Value.Message ?? string.Empty);
                                    if (shouldHandle)
                                    {
                                        await liveCommentService.ProcessCommentAsync(
                                            change.Value.CommentId,
                                            change.Value.From?.Id ?? string.Empty,
                                            change.Value.Message ?? string.Empty,
                                            change.Value.PostId ?? string.Empty);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    commentLogger.LogError(ex, "Error processing live comment {CommentId}", change.Value.CommentId);
                                }
                                finally
                                {
                                    liveCommentSemaphore.Release();
                                }
                            });
                            eventCount++;
                        }
                    }
                }
            }

            logger.LogInformation("Webhook received: {EventCount} events queued", eventCount);
            return Results.Ok(new { status = "EVENT_RECEIVED" });
        })
        .WithName("ReceiveWebhook");
    }

    internal static void MapHealthCheckEndpoint(this WebApplication app)
    {
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        data = e.Value.Data
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                });
                await context.Response.WriteAsync(result);
            }
        });
    }

    internal static void MapChannelQueueMetricsEndpoint(this WebApplication app)
    {
        app.MapGet("/metrics", (Channel<MessagingEvent> channel) =>
        {
            var queueDepth = channel.Reader.Count;
            return Results.Ok(new
            {
                queue_depth = queueDepth,
                queue_capacity = 1000,
                queue_utilization_percent = queueDepth * 100.0 / 1000,
                timestamp = DateTimeOffset.UtcNow
            });
        });
    }
}
