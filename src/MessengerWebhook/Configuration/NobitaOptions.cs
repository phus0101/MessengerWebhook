namespace MessengerWebhook.Configuration;

public class NobitaOptions
{
    public const string SectionName = "Nobita";

    public string BaseUrl { get; set; } = "https://testing.ecrm.vn/public-api/v1/";
    public string ApiKey { get; set; } = string.Empty;
    public bool EnableCustomerInsightLookup { get; set; }
    public bool EnableDirectOrderSubmission { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
}
