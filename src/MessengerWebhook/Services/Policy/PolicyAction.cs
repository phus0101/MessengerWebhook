namespace MessengerWebhook.Services.Policy;

public enum PolicyAction
{
    Allow = 0,
    SafeReply = 1,
    SoftEscalate = 2,
    HardEscalate = 3
}
