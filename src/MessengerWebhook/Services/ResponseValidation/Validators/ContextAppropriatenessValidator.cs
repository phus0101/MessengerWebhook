using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.ResponseValidation.Models;

namespace MessengerWebhook.Services.ResponseValidation.Validators;

/// <summary>
/// Validates context appropriateness in bot responses
/// </summary>
public class ContextAppropriatenessValidator
{
    public List<ValidationIssue> Validate(string response, ConversationContext context)
    {
        var issues = new List<ValidationIssue>();

        // Check if response is too pushy for Browsing stage
        if (context.CurrentStage == JourneyStage.Browsing)
        {
            var pushyPhrases = new[] { "đặt hàng ngay", "mua ngay", "order now", "đặt luôn" };
            foreach (var phrase in pushyPhrases)
            {
                if (response.ToLower().Contains(phrase))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Category = "Context",
                        Message = $"Pushy phrase '{phrase}' detected during Browsing stage",
                        SuggestedFix = "Use softer language like 'tham khảo thêm' or 'xem thêm'"
                    });
                    break;
                }
            }
        }

        // Check if response lacks urgency for Ready stage
        if (context.CurrentStage == JourneyStage.Ready)
        {
            var hasCallToAction = response.Contains("đặt hàng") ||
                                  response.Contains("order") ||
                                  response.Contains("mua") ||
                                  response.Contains("thanh toán");

            if (!hasCallToAction && response.Length > 50)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Info,
                    Category = "Context",
                    Message = "Customer is Ready but response lacks clear call-to-action",
                    SuggestedFix = "Include purchase guidance or order instructions"
                });
            }
        }

        return issues;
    }
}
