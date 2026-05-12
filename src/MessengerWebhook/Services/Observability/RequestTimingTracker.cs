using System.Diagnostics;

namespace MessengerWebhook.Services.Observability;

/// <summary>
/// Tracks per-request timing with named checkpoints for latency breakdown analysis.
/// Instantiate once per ProcessAsync call; not a DI service.
/// </summary>
public sealed class RequestTimingTracker
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly Dictionary<string, long> _checkpoints = new();

    /// <summary>Records elapsed ms at this checkpoint label.</summary>
    public void Mark(string name) => _checkpoints[name] = _sw.ElapsedMilliseconds;

    public long ElapsedMs => _sw.ElapsedMilliseconds;

    /// <summary>Returns checkpoint snapshot safe to pass to structured logger.</summary>
    public IReadOnlyDictionary<string, long> Checkpoints => _checkpoints;
}
