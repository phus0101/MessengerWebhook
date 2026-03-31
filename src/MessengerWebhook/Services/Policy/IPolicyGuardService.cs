namespace MessengerWebhook.Services.Policy;

public interface IPolicyGuardService
{
    PolicyDecision Evaluate(string message);
    string EnsureClosingCallToAction(string response);
}
