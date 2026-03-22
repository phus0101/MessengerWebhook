using MessengerWebhook.Data.Entities;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.StateMachine.Handlers;

public interface IStateHandler
{
    ConversationState HandledState { get; }
    Task<string> HandleAsync(StateContext ctx, string message);
}
