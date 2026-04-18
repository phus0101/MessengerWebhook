namespace MessengerWebhook.Services.ABTesting;

public interface IABTestService
{
    /// <summary>
    /// Gets the A/B test variant for a given PSID and session.
    /// Returns "control" or "treatment" based on deterministic hash assignment.
    /// </summary>
    Task<string> GetVariantAsync(string psid, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if A/B testing is currently enabled.
    /// </summary>
    bool IsEnabled();
}
