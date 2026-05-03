namespace MessengerWebhook.Services.Policy;

public enum PolicySemanticIntent
{
    None = 0,
    ManualReview = 1,
    UnsupportedQuestion = 2,
    PolicyException = 3,
    RefundRequest = 4,
    CancellationRequest = 5,
    PromptInjection = 6
}
