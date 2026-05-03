namespace MessengerWebhook.Services.Policy;

public interface IPolicySignalDetector
{
    IReadOnlyList<PolicySignal> Detect(PolicyGuardRequest request, string normalizedMessage);
}
