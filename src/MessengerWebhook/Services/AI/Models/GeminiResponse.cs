namespace MessengerWebhook.Services.AI.Models;

public class GeminiResponse
{
    public Candidate[]? Candidates { get; set; }
    public UsageMetadata? UsageMetadata { get; set; }
}

public class Candidate
{
    public Content? Content { get; set; }
    public string? FinishReason { get; set; }
}

public class Content
{
    public Part[]? Parts { get; set; }
    public string? Role { get; set; }
}

public class UsageMetadata
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int TotalTokenCount { get; set; }
}
