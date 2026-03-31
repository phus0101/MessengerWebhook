using MessengerWebhook.Data.Entities;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.DraftOrders;

public interface IDraftOrderService
{
    Task<DraftOrder> CreateFromContextAsync(StateContext context, CancellationToken cancellationToken = default);
}
