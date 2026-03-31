using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.StateMachine;

public interface IStateMachine
{
    Task<StateContext> LoadOrCreateAsync(string psid);
    Task<StateContext> LoadOrCreateAsync(string psid, string? pageId);
    Task<bool> TransitionToAsync(StateContext ctx, ConversationState newState);
    Task SaveAsync(StateContext ctx);
    Task<string> ProcessMessageAsync(string psid, string message);
    Task<string> ProcessMessageAsync(string psid, string message, string? pageId);
    Task ResetAsync(string psid);
}
