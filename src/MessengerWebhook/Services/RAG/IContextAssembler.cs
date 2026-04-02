namespace MessengerWebhook.Services.RAG;

public interface IContextAssembler
{
    Task<string> AssembleContextAsync(
        List<string> productIds,
        CancellationToken cancellationToken = default);
}
