namespace MessengerWebhook.Services.ABTesting.Configuration;

public class ABTestingOptions
{
    public const string SectionName = "ABTesting";

    /// <summary>
    /// Enable/disable A/B testing globally. When false, all users get treatment (full pipeline).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Percentage of users assigned to treatment group (0-100).
    /// Default: 50 (50% treatment, 50% control).
    /// </summary>
    public int TreatmentPercentage { get; set; } = 50;

    /// <summary>
    /// Seed for hash-based assignment. Change to re-randomize assignments.
    /// </summary>
    public string HashSeed { get; set; } = "ab-test-2026";
}
