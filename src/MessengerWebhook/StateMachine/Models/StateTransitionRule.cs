using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.StateMachine.Models;

public class StateTransitionRule
{
    public ConversationState FromState { get; set; }
    public ConversationState ToState { get; set; }
    public Func<StateContext, bool>? Condition { get; set; }

    public bool CanTransition(StateContext context)
    {
        if (context.CurrentState != FromState)
        {
            return false;
        }

        return Condition?.Invoke(context) ?? true;
    }
}
