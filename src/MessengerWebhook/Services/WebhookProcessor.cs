using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services.QuickReply;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services;

/// <summary>
/// Processes webhook events with idempotency checks and page-aware tenant resolution.
/// </summary>
public class WebhookProcessor
{
    private readonly IMemoryCache _cache;
    private readonly IMessengerService _messengerService;
    private readonly IStateMachine _stateMachine;
    private readonly IQuickReplyHandler _quickReplyHandler;
    private readonly MessengerBotDbContext? _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IBotLockService _botLockService;
    private readonly IHostEnvironment? _environment;
    private readonly AdminOptions _adminOptions;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IMemoryCache cache,
        IMessengerService messengerService,
        IStateMachine stateMachine,
        IQuickReplyHandler quickReplyHandler,
        ILogger<WebhookProcessor> logger)
        : this(
            cache,
            messengerService,
            stateMachine,
            quickReplyHandler,
            null,
            new NullTenantContext(),
            new NoOpBotLockService(),
            null,
            Options.Create(new AdminOptions()),
            logger)
    {
    }

    public WebhookProcessor(
        IMemoryCache cache,
        IMessengerService messengerService,
        IStateMachine stateMachine,
        IQuickReplyHandler quickReplyHandler,
        MessengerBotDbContext? dbContext,
        ITenantContext tenantContext,
        IBotLockService botLockService,
        IHostEnvironment? environment,
        IOptions<AdminOptions> adminOptions,
        ILogger<WebhookProcessor> logger)
    {
        _cache = cache;
        _messengerService = messengerService;
        _stateMachine = stateMachine;
        _quickReplyHandler = quickReplyHandler;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _botLockService = botLockService;
        _environment = environment;
        _adminOptions = adminOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(MessagingEvent messagingEvent)
    {
        await InitializeTenantContextAsync(messagingEvent.Recipient.Id);

        if (messagingEvent.Message != null)
        {
            await ProcessMessageAsync(messagingEvent);
            return;
        }

        if (messagingEvent.Postback != null)
        {
            await ProcessPostbackAsync(messagingEvent);
            return;
        }

        _logger.LogWarning("Unknown event type received");
    }

    private async Task ProcessMessageAsync(MessagingEvent evt)
    {
        var messageId = evt.Message!.Mid;
        var cacheKey = $"msg:{messageId}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogInformation("Duplicate message ignored: {MessageId}", messageId);
            return;
        }

        var senderId = evt.Sender.Id;
        var pageId = evt.Recipient.Id;
        var text = evt.Message.Text;

        if (await _botLockService.IsLockedAsync(senderId))
        {
            _logger.LogInformation("Bot reply skipped because conversation is locked for {SenderId}", senderId);
            MarkProcessed(cacheKey);
            return;
        }

        if (evt.Message.QuickReply != null)
        {
            _logger.LogInformation(
                "Processing Quick Reply from {SenderId} on page {PageId}: {Payload}",
                senderId,
                pageId,
                evt.Message.QuickReply.Payload);

            try
            {
                var reply = await _quickReplyHandler.HandleQuickReplyAsync(
                    senderId,
                    evt.Message.QuickReply.Payload,
                    pageId);
                await _messengerService.SendTextMessageAsync(senderId, reply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Quick Reply for {SenderId}", senderId);
                await _messengerService.SendTextMessageAsync(
                    senderId,
                    "Dạ em đang gặp sự cố kỹ thuật. Chị nhắn lại giúp em sau ít phút nha.");
            }

            MarkProcessed(cacheKey);
            return;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation(
                "Processing message from {SenderId} on page {PageId}: {Text}",
                senderId,
                pageId,
                text);

            try
            {
                var reply = await _stateMachine.ProcessMessageAsync(senderId, text, pageId);
                await _messengerService.SendTextMessageAsync(senderId, reply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message for {SenderId}", senderId);
                await _messengerService.SendTextMessageAsync(
                    senderId,
                    "Dạ em đang gặp sự cố kỹ thuật. Chị nhắn lại giúp em sau ít phút nha.");
            }
        }

        MarkProcessed(cacheKey);
    }

    private async Task ProcessPostbackAsync(MessagingEvent evt)
    {
        var cacheKey = $"postback:{evt.Sender.Id}:{evt.Timestamp}:{evt.Postback!.Payload}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogInformation("Duplicate postback ignored: {SenderId}:{Payload}", evt.Sender.Id, evt.Postback.Payload);
            return;
        }

        var senderId = evt.Sender.Id;
        var pageId = evt.Recipient.Id;

        if (await _botLockService.IsLockedAsync(senderId))
        {
            _logger.LogInformation("Bot postback reply skipped because conversation is locked for {SenderId}", senderId);
            MarkProcessed(cacheKey);
            return;
        }

        try
        {
            var reply = await _quickReplyHandler.HandlePostbackAsync(senderId, evt.Postback.Payload, pageId);
            await _messengerService.SendTextMessageAsync(senderId, reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Postback for {SenderId}", senderId);
            await _messengerService.SendTextMessageAsync(
                senderId,
                "Dạ em đang gặp sự cố kỹ thuật. Chị nhắn lại giúp em sau ít phút nha.");
        }

        MarkProcessed(cacheKey);
    }

    private async Task InitializeTenantContextAsync(string? pageId)
    {
        _tenantContext.Clear();

        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        if (_dbContext == null)
        {
            _tenantContext.Initialize(null, pageId, null);
            return;
        }

        var pageConfig = await _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FacebookPageId == pageId && x.IsActive);

        if (pageConfig == null)
        {
            pageConfig = await TryAdoptUnknownDevelopmentPageAsync(pageId);
        }

        _tenantContext.Initialize(pageConfig?.TenantId, pageId, pageConfig?.DefaultManagerEmail);
    }

    private async Task<FacebookPageConfig?> TryAdoptUnknownDevelopmentPageAsync(string pageId)
    {
        if (_dbContext == null ||
            _environment?.IsDevelopment() != true ||
            !_adminOptions.AllowTenantWideVisibilityInDevelopment ||
            string.IsNullOrWhiteSpace(_adminOptions.BootstrapEmail))
        {
            return null;
        }

        var bootstrapPageConfig = await _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.DefaultManagerEmail == _adminOptions.BootstrapEmail && x.IsActive);
        if (bootstrapPageConfig?.TenantId == null)
        {
            return null;
        }

        var adoptedConfig = new FacebookPageConfig
        {
            TenantId = bootstrapPageConfig.TenantId,
            FacebookPageId = pageId,
            PageName = $"Imported Dev Page {pageId}",
            DefaultManagerEmail = _adminOptions.BootstrapEmail,
            IsPrimaryPage = false,
            IsActive = true
        };

        _dbContext.FacebookPageConfigs.Add(adoptedConfig);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Adopted unknown Facebook page {PageId} into bootstrap tenant {TenantId} during webhook processing",
            pageId,
            bootstrapPageConfig.TenantId);

        return adoptedConfig;
    }

    private void MarkProcessed(string cacheKey)
    {
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48),
            Size = 1
        });
    }

    private sealed class NoOpBotLockService : IBotLockService
    {
        public Task<bool> IsLockedAsync(string facebookPsid, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task LockAsync(string facebookPsid, string reason, Guid? supportCaseId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReleaseAsync(string facebookPsid, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExtendLockAsync(string facebookPsid, int additionalMinutes, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<BotConversationLock>> GetActiveLocksAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<BotConversationLock>());
        public Task<List<BotConversationLock>> GetLockHistoryAsync(string facebookPsid, CancellationToken cancellationToken = default) => Task.FromResult(new List<BotConversationLock>());
    }
}
