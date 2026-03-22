using System.Text.Json;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine;

public class ConversationStateMachine : IStateMachine
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<ConversationStateMachine> _logger;
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromMinutes(60);

    public ConversationStateMachine(
        ISessionRepository sessionRepository,
        ILogger<ConversationStateMachine> logger)
    {
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<StateContext> LoadOrCreateAsync(string psid)
    {
        var session = await _sessionRepository.GetByPSIDAsync(psid);

        if (session == null)
        {
            _logger.LogInformation("Creating new session for PSID: {PSID}", psid);
            session = new ConversationSession
            {
                FacebookPSID = psid,
                CurrentState = ConversationState.Idle,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(AbsoluteTimeout)
            };
            session = await _sessionRepository.CreateAsync(session);
        }

        var context = MapToContext(session);

        // Check for timeout
        if (context.IsTimedOut(InactivityTimeout) || IsAbsoluteTimeout(session))
        {
            _logger.LogInformation("Session timeout detected for PSID: {PSID}, resetting to Idle", psid);
            context.CurrentState = ConversationState.Idle;
            context.Data.Clear();
            context.LastInteractionAt = DateTime.UtcNow;

            // Persist the reset state to database
            session.CurrentState = ConversationState.Idle;
            session.ContextJson = null;
            session.LastActivityAt = DateTime.UtcNow;
            session.ExpiresAt = DateTime.UtcNow.Add(AbsoluteTimeout);
            await _sessionRepository.UpdateAsync(session);
        }

        return context;
    }

    public async Task<bool> TransitionToAsync(StateContext ctx, ConversationState newState)
    {
        if (ctx.CurrentState == newState)
        {
            _logger.LogDebug("Already in state {State} for PSID: {PSID}", newState, ctx.FacebookPSID);
            return true;
        }

        if (!StateTransitionRules.IsValidTransition(ctx.CurrentState, newState, ctx))
        {
            _logger.LogWarning(
                "Invalid state transition from {FromState} to {ToState} for PSID: {PSID}",
                ctx.CurrentState,
                newState,
                ctx.FacebookPSID);
            return false;
        }

        var oldState = ctx.CurrentState;
        ctx.CurrentState = newState;
        ctx.LastInteractionAt = DateTime.UtcNow;

        _logger.LogInformation(
            "State transition: {FromState} -> {ToState} for PSID: {PSID}",
            oldState,
            newState,
            ctx.FacebookPSID);

        return true;
    }

    public async Task SaveAsync(StateContext ctx)
    {
        var session = await _sessionRepository.GetByPSIDAsync(ctx.FacebookPSID);

        if (session == null)
        {
            _logger.LogError("Session not found for PSID: {PSID}", ctx.FacebookPSID);
            return;
        }

        session.CurrentState = ctx.CurrentState;
        session.LastActivityAt = ctx.LastInteractionAt;
        session.ContextJson = JsonSerializer.Serialize(ctx.Data);
        session.ExpiresAt = DateTime.UtcNow.Add(AbsoluteTimeout);

        await _sessionRepository.UpdateAsync(session);
        _logger.LogDebug("Session saved for PSID: {PSID}", ctx.FacebookPSID);
    }

    public async Task<string> ProcessMessageAsync(string psid, string message)
    {
        var context = await LoadOrCreateAsync(psid);

        // Stub implementation - will be replaced with state handlers in Phase 3.2
        _logger.LogInformation(
            "Processing message in state {State} for PSID: {PSID}",
            context.CurrentState,
            psid);

        // For now, just transition from Idle to Greeting
        if (context.CurrentState == ConversationState.Idle)
        {
            await TransitionToAsync(context, ConversationState.Greeting);
            await SaveAsync(context);
            return "Hello! Welcome to our cosmetics store. How can I help you today?";
        }

        await SaveAsync(context);
        return "Message received. State handlers will be implemented in Phase 3.2.";
    }

    public async Task ResetAsync(string psid)
    {
        var session = await _sessionRepository.GetByPSIDAsync(psid);

        if (session == null)
        {
            _logger.LogWarning("Cannot reset - session not found for PSID: {PSID}", psid);
            return;
        }

        session.CurrentState = ConversationState.Idle;
        session.ContextJson = null;
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.Add(AbsoluteTimeout);

        await _sessionRepository.UpdateAsync(session);
        _logger.LogInformation("Session reset to Idle for PSID: {PSID}", psid);
    }

    private StateContext MapToContext(ConversationSession session)
    {
        var context = new StateContext
        {
            SessionId = session.Id,
            FacebookPSID = session.FacebookPSID,
            CurrentState = session.CurrentState,
            LastInteractionAt = session.LastActivityAt,
            CreatedAt = session.CreatedAt
        };

        if (!string.IsNullOrEmpty(session.ContextJson))
        {
            try
            {
                context.Data = JsonSerializer.Deserialize<Dictionary<string, object>>(session.ContextJson)
                    ?? new Dictionary<string, object>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize context JSON for PSID: {PSID}", session.FacebookPSID);
                context.Data = new Dictionary<string, object>();
            }
        }

        return context;
    }

    private bool IsAbsoluteTimeout(ConversationSession session)
    {
        if (session.ExpiresAt == null)
        {
            return false;
        }

        return DateTime.UtcNow > session.ExpiresAt.Value;
    }
}
