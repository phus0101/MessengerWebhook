using System.Text.Json;
using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.StateMachine.Models;

public class StateContext
{
    public string SessionId { get; set; } = string.Empty;
    public string FacebookPSID { get; set; } = string.Empty;
    public ConversationState CurrentState { get; set; } = ConversationState.Idle;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime LastInteractionAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public T? GetData<T>(string key)
    {
        if (!Data.TryGetValue(key, out var value))
        {
            return default;
        }

        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        try
        {
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    public void SetData(string key, object value)
    {
        Data[key] = value;
    }

    public bool IsTimedOut(TimeSpan timeout)
    {
        return DateTime.UtcNow - LastInteractionAt > timeout;
    }
}
