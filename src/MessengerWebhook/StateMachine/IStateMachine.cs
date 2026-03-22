using MessengerWebhook.Data.Entities;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.StateMachine;

public interface IStateMachine
{
    Task<StateContext> LoadOrCreateAsync(string psid);
    Task<bool> TransitionToAsync(StateContext ctx, ConversationState newState);
    Task SaveAsync(StateContext ctx);
    Task<string> ProcessMessageAsync(string psid, string message);
    Task ResetAsync(string psid);
}
