using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Sales.Context;

/// <summary>
/// A product candidate extracted from conversation history with its source role and message.
/// </summary>
public sealed record HistoryProductCandidate(Product Product, string Role, string Message);
