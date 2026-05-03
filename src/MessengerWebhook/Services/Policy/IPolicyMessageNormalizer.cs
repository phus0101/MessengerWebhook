namespace MessengerWebhook.Services.Policy;

public interface IPolicyMessageNormalizer
{
    string Normalize(string message);
}
