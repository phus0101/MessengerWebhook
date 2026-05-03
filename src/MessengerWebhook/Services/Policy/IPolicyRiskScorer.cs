namespace MessengerWebhook.Services.Policy;

public interface IPolicyRiskScorer
{
    PolicyScoreResult Score(
        PolicyGuardRequest request,
        IReadOnlyList<PolicySignal> signals,
        PolicyClassificationResult? classification = null);
}
