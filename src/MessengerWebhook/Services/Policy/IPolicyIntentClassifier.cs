namespace MessengerWebhook.Services.Policy;

public interface IPolicyIntentClassifier
{
    Task<PolicyClassificationResult?> ClassifyAsync(
        PolicyGuardRequest request,
        string normalizedMessage,
        CancellationToken cancellationToken = default);
}
