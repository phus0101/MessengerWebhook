using Serilog.Core;
using Serilog.Events;

namespace MessengerWebhook.Services.Observability;

/// <summary>
/// Serilog enricher that auto-redacts PII (phone numbers, addresses) from string log properties.
/// Acts as defense-in-depth safety net — primary protection is at call sites via PiiRedactor.
/// </summary>
public sealed class PiiRedactingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Collect mutations first to avoid modifying dictionary during iteration.
        // Null until first PII match — zero allocation on the common (no-PII) path.
        List<(string key, string redacted)>? mutations = null;

        foreach (var kvp in logEvent.Properties)
        {
            if (kvp.Value is not ScalarValue { Value: string text })
                continue;

            var redacted = PiiRedactor.Redact(text);
            if (redacted != text)
                (mutations ??= []).Add((kvp.Key, redacted));
        }

        if (mutations is null) return;

        foreach (var (key, redacted) in mutations)
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, redacted));
    }
}
