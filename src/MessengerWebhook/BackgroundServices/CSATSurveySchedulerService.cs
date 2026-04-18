using MessengerWebhook.Services.Survey;
using System.Collections.Concurrent;

namespace MessengerWebhook.BackgroundServices;

/// <summary>
/// Background service that schedules and sends CSAT surveys after a delay
/// </summary>
public class CSATSurveySchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CSATSurveySchedulerService> _logger;
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _scheduledSurveys = new();

    public CSATSurveySchedulerService(
        IServiceProvider serviceProvider,
        ILogger<CSATSurveySchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CSATSurveySchedulerService started");

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public static void ScheduleSurvey(string sessionId, TimeSpan delay, IServiceProvider serviceProvider, ILogger logger)
    {
        // Cancel existing survey for this session if any
        if (_scheduledSurveys.TryRemove(sessionId, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _scheduledSurveys[sessionId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Survey scheduled for session {SessionId} with {Delay}min delay", sessionId, delay.TotalMinutes);
                await Task.Delay(delay, cts.Token);

                using var scope = serviceProvider.CreateScope();
                var surveyService = scope.ServiceProvider.GetRequiredService<ICSATSurveyService>();

                await surveyService.SendSurveyAsync(sessionId);
                logger.LogInformation("Survey sent for session {SessionId}", sessionId);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Survey cancelled for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending survey for session {SessionId}", sessionId);
            }
            finally
            {
                _scheduledSurveys.TryRemove(sessionId, out _);
                cts.Dispose();
            }
        }, cts.Token);
    }

    public override void Dispose()
    {
        // Cancel all pending surveys
        foreach (var cts in _scheduledSurveys.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _scheduledSurveys.Clear();

        base.Dispose();
    }
}
