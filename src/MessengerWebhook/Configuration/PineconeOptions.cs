namespace MessengerWebhook.Configuration;

public class PineconeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Environment { get; set; } = "us-east-1";
    public string IndexName { get; set; } = "messenger-products";
    public int TimeoutSeconds { get; set; } = 10;
}
