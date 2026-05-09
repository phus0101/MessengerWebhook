namespace MessengerWebhook.Configuration;

public class SeqOptions
{
    public const string SectionName = "Seq";

    public bool Enabled { get; init; } = false;
    public string ServerUrl { get; init; } = "http://localhost:5341";
    public string? ApiKey { get; init; }

    // Seq OTLP endpoint for distributed tracing (optional)
    public string? OtlpEndpoint { get; init; }
}
