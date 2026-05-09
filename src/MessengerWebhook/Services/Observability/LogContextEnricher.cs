using Serilog.Context;

namespace MessengerWebhook.Services.Observability;

public static class LogContextEnricher
{
    /// <summary>
    /// Pushes TenantId (and optionally PsidHash) into Serilog LogContext for the current scope.
    /// Dispose the returned handle to pop the properties.
    /// </summary>
    public static IDisposable PushTenantContext(string tenantId, string? psid = null)
    {
        var disposables = new List<IDisposable>
        {
            LogContext.PushProperty("TenantId", tenantId)
        };

        if (!string.IsNullOrWhiteSpace(psid))
        {
            disposables.Add(LogContext.PushProperty("PsidHash", PiiRedactor.HashPsid(psid)));
        }

        return new CompositeDisposable(disposables);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _items;

        public CompositeDisposable(IReadOnlyList<IDisposable> items) => _items = items;

        public void Dispose()
        {
            // Dispose in reverse order (LIFO — mirrors LogContext stack semantics)
            for (var i = _items.Count - 1; i >= 0; i--)
                _items[i].Dispose();
        }
    }
}
