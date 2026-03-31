using System.Text.Json;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine;

public class ConversationStateMachine : IStateMachine
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ICustomerIntelligenceService _customerIntelligenceService;
    private readonly ILogger<ConversationStateMachine> _logger;
    private readonly Dictionary<ConversationState, IStateHandler> _handlers;
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromMinutes(60);

    public ConversationStateMachine(
        ISessionRepository sessionRepository,
        IEnumerable<IStateHandler> handlers,
        ILogger<ConversationStateMachine> logger)
        : this(sessionRepository, handlers, new NullCustomerIntelligenceService(), logger)
    {
    }

    public ConversationStateMachine(
        ISessionRepository sessionRepository,
        IEnumerable<IStateHandler> handlers,
        ICustomerIntelligenceService customerIntelligenceService,
        ILogger<ConversationStateMachine> logger)
    {
        _sessionRepository = sessionRepository;
        _customerIntelligenceService = customerIntelligenceService;
        _logger = logger;
        _handlers = handlers.ToDictionary(h => h.HandledState, h => h);
    }

    public Task<StateContext> LoadOrCreateAsync(string psid)
    {
        return LoadOrCreateAsync(psid, null);
    }

    public async Task<StateContext> LoadOrCreateAsync(string psid, string? pageId)
    {
        var session = await _sessionRepository.GetByPSIDAsync(psid);

        if (session == null)
        {
            _logger.LogInformation("Creating new session for PSID: {PSID}", psid);
            session = new ConversationSession
            {
                FacebookPSID = psid,
                FacebookPageId = pageId,
                CurrentState = ConversationState.Idle,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(AbsoluteTimeout)
            };
            session = await _sessionRepository.CreateAsync(session);
        }
        else if (!string.IsNullOrWhiteSpace(pageId) && string.IsNullOrWhiteSpace(session.FacebookPageId))
        {
            session.FacebookPageId = pageId;
            await _sessionRepository.UpdateAsync(session);
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

        await HydrateCustomerMemoryAsync(context, pageId ?? session.FacebookPageId);
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

    public Task<string> ProcessMessageAsync(string psid, string message)
    {
        return ProcessMessageAsync(psid, message, null);
    }

    public async Task<string> ProcessMessageAsync(string psid, string message, string? pageId)
    {
        var context = await LoadOrCreateAsync(psid, pageId);
        if (!string.IsNullOrWhiteSpace(pageId))
        {
            context.SetData("facebookPageId", pageId);
        }

        _logger.LogInformation(
            "Processing message in state {State} for PSID: {PSID}",
            context.CurrentState,
            psid);

        // Get handler for current state
        if (!_handlers.TryGetValue(context.CurrentState, out var handler))
        {
            _logger.LogError("No handler found for state {State}", context.CurrentState);
            await SaveAsync(context);
            return "Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại sau.";
        }

        // Process message with handler
        var response = await handler.HandleAsync(context, message);

        // Save updated context
        await SaveAsync(context);

        return response;
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

        if (!string.IsNullOrWhiteSpace(session.FacebookPageId) &&
            !context.Data.ContainsKey("facebookPageId"))
        {
            context.SetData("facebookPageId", session.FacebookPageId);
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

    private async Task HydrateCustomerMemoryAsync(StateContext context, string? pageId)
    {
        if (!string.IsNullOrWhiteSpace(context.GetData<string>("customerPhone")) &&
            !string.IsNullOrWhiteSpace(context.GetData<string>("shippingAddress")))
        {
            return;
        }

        var customer = await _customerIntelligenceService.GetExistingAsync(
            context.FacebookPSID,
            pageId);
        if (customer == null)
        {
            return;
        }

        var hydratedAnyField = false;

        if (string.IsNullOrWhiteSpace(context.GetData<string>("customerName")) &&
            !string.IsNullOrWhiteSpace(customer.FullName))
        {
            context.SetData("customerName", customer.FullName);
            context.SetData("rememberedCustomerName", customer.FullName);
        }

        if (string.IsNullOrWhiteSpace(context.GetData<string>("customerPhone")) &&
            !string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            context.SetData("customerPhone", customer.PhoneNumber);
            context.SetData("rememberedCustomerPhone", customer.PhoneNumber);
            hydratedAnyField = true;
        }

        if (string.IsNullOrWhiteSpace(context.GetData<string>("shippingAddress")) &&
            !string.IsNullOrWhiteSpace(customer.ShippingAddress))
        {
            context.SetData("shippingAddress", customer.ShippingAddress);
            context.SetData("rememberedShippingAddress", customer.ShippingAddress);
            hydratedAnyField = true;
        }

        if (!hydratedAnyField)
        {
            return;
        }

        context.SetData("rememberedCustomerLastInteractionAt", customer.LastInteractionAt);
        context.SetData("contactMemorySource", "customer-identity");
        context.SetData("contactNeedsConfirmation", true);
    }

    private sealed class NullCustomerIntelligenceService : ICustomerIntelligenceService
    {
        public Task<CustomerIdentity?> GetExistingAsync(
            string facebookPsid,
            string? pageId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CustomerIdentity?>(null);
        }

        public Task<CustomerIdentity> GetOrCreateAsync(
            string facebookPsid,
            string? pageId = null,
            string? phoneNumber = null,
            string? fullName = null,
            string? shippingAddress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CustomerIdentity
            {
                FacebookPSID = facebookPsid,
                FacebookPageId = pageId,
                PhoneNumber = phoneNumber,
                FullName = fullName,
                ShippingAddress = shippingAddress
            });
        }

        public Task<VipProfile> GetVipProfileAsync(CustomerIdentity customer, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VipProfile { CustomerIdentityId = customer.Id });
        }

        public Task<RiskSignal> BuildRiskSignalAsync(CustomerIdentity customer, Guid? draftOrderId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RiskSignal { CustomerIdentityId = customer.Id, DraftOrderId = draftOrderId });
        }
    }
}
