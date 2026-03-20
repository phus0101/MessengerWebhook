namespace MessengerWebhook.Services.AI.Models;

public class GeminiRequest
{
    public object[] Contents { get; set; } = Array.Empty<object>();
    public SystemInstruction? SystemInstruction { get; set; }
    public GenerationConfig? GenerationConfig { get; set; }
}

public class SystemInstruction
{
    public Part[] Parts { get; set; } = Array.Empty<Part>();
}

public class GenerationConfig
{
    public double Temperature { get; set; }
    public int MaxOutputTokens { get; set; }
}

public class Part
{
    public string Text { get; set; } = string.Empty;
}
