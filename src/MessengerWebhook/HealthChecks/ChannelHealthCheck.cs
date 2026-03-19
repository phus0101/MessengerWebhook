using System.Threading.Channels;
using MessengerWebhook.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MessengerWebhook.HealthChecks;

public class ChannelHealthCheck : IHealthCheck
{
    private readonly Channel<MessagingEvent> _channel;

    public ChannelHealthCheck(Channel<MessagingEvent> channel)
    {
        _channel = channel;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var queueDepth = _channel.Reader.Count;
        var capacity = 1000; // From Program.cs BoundedChannelOptions

        var data = new Dictionary<string, object>
        {
            ["queue_depth"] = queueDepth,
            ["capacity"] = capacity,
            ["utilization_percent"] = (queueDepth * 100.0 / capacity)
        };

        if (queueDepth >= capacity * 0.9)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Queue near capacity: {queueDepth}/{capacity}",
                data: data));
        }

        if (queueDepth >= capacity * 0.7)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Queue utilization high: {queueDepth}/{capacity}",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Queue healthy: {queueDepth}/{capacity}",
            data: data));
    }
}
