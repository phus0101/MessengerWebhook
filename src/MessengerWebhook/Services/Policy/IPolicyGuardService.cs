namespace MessengerWebhook.Services.Policy;

public interface IPolicyGuardService
{
    PolicyDecision Evaluate(string message);
    ValueTask<PolicyDecision> EvaluateAsync(PolicyGuardRequest request, CancellationToken cancellationToken = default);
    string EnsureClosingCallToAction(string response);
}
