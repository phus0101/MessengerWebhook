namespace MessengerWebhook.Services.Survey;

public interface ICSATSurveyService
{
    Task SendSurveyAsync(string sessionId);
    Task HandleRatingAsync(string psid, int rating);
    Task HandleFeedbackAsync(string psid, string feedbackText);
}
