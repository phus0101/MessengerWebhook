using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace MessengerWebhook.UnitTests.StateMachine;

public class ConversationStateMachineTests
{
    private readonly Mock<ISessionRepository> _mockRepository;
    private readonly Mock<ICustomerIntelligenceService> _mockCustomerIntelligenceService;
    private readonly Mock<ILogger<ConversationStateMachine>> _mockLogger;
    private readonly ConversationStateMachine _stateMachine;

    public ConversationStateMachineTests()
    {
        _mockRepository = new Mock<ISessionRepository>();
        _mockCustomerIntelligenceService = new Mock<ICustomerIntelligenceService>();
        _mockLogger = new Mock<ILogger<ConversationStateMachine>>();

        // Create empty handler collection for unit tests (handlers tested separately)
        var handlers = Enumerable.Empty<IStateHandler>();

        _mockCustomerIntelligenceService
            .Setup(x => x.GetExistingAsync(It.IsAny<string>(), It.IsAny<string?>(), default))
            .ReturnsAsync((CustomerIdentity?)null);

        _stateMachine = new ConversationStateMachine(
            _mockRepository.Object,
            handlers,
            _mockCustomerIntelligenceService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task LoadOrCreateAsync_CreatesNewSession_WhenNotExists()
    {
        // Arrange
        var psid = "test-psid-123";
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync((ConversationSession?)null);
        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<ConversationSession>()))
            .ReturnsAsync((ConversationSession s) => s);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(psid, context.FacebookPSID);
        Assert.Equal(ConversationState.Idle, context.CurrentState);
        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<ConversationSession>()), Times.Once);
    }

    [Fact]
    public async Task LoadOrCreateAsync_LoadsExistingSession_WhenExists()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.MainMenu,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(55)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(psid, context.FacebookPSID);
        Assert.Equal(ConversationState.MainMenu, context.CurrentState);
        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<ConversationSession>()), Times.Never);
    }

    [Fact]
    public async Task LoadOrCreateAsync_ResetsToIdle_WhenInactivityTimeoutExceeded()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.BrowsingProducts,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-20), // 20 minutes ago (exceeds 15min timeout)
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(40)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.Equal(ConversationState.Idle, context.CurrentState);
        Assert.Empty(context.Data);
    }

    [Fact]
    public async Task LoadOrCreateAsync_ResetsToIdle_WhenAbsoluteTimeoutExceeded()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.CartReview,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired 5 minutes ago
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.Equal(ConversationState.Idle, context.CurrentState);
        Assert.Empty(context.Data);
    }

    [Fact]
    public async Task TransitionToAsync_AllowsValidTransition()
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.Idle
        };

        // Act
        var result = await _stateMachine.TransitionToAsync(context, ConversationState.Greeting);

        // Assert
        Assert.True(result);
        Assert.Equal(ConversationState.Greeting, context.CurrentState);
    }

    [Fact]
    public async Task TransitionToAsync_RejectsInvalidTransition()
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.Idle
        };

        // Act
        var result = await _stateMachine.TransitionToAsync(context, ConversationState.OrderPlaced);

        // Assert
        Assert.False(result);
        Assert.Equal(ConversationState.Idle, context.CurrentState);
    }

    [Fact]
    public async Task TransitionToAsync_AllowsSameStateTransition()
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.MainMenu
        };

        // Act
        var result = await _stateMachine.TransitionToAsync(context, ConversationState.MainMenu);

        // Assert
        Assert.True(result);
        Assert.Equal(ConversationState.MainMenu, context.CurrentState);
    }

    [Fact]
    public async Task TransitionToAsync_RespectsConditionalRules()
    {
        // Arrange - CartReview to ShippingAddress requires cart items
        var contextWithoutCart = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CartReview
        };

        // Act
        var result = await _stateMachine.TransitionToAsync(contextWithoutCart, ConversationState.ShippingAddress);

        // Assert
        Assert.False(result);
        Assert.Equal(ConversationState.CartReview, contextWithoutCart.CurrentState);
    }

    [Fact]
    public async Task TransitionToAsync_AllowsTransitionWhenConditionMet()
    {
        // Arrange - CartReview to ShippingAddress with cart items
        var contextWithCart = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CartReview
        };
        contextWithCart.SetData("cartItems", new List<string> { "item1", "item2" });

        // Act
        var result = await _stateMachine.TransitionToAsync(contextWithCart, ConversationState.ShippingAddress);

        // Assert
        Assert.True(result);
        Assert.Equal(ConversationState.ShippingAddress, contextWithCart.CurrentState);
    }

    [Fact]
    public async Task SaveAsync_UpdatesSessionInRepository()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.Idle,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        var context = new StateContext
        {
            SessionId = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.MainMenu,
            LastInteractionAt = DateTime.UtcNow
        };
        context.SetData("testKey", "testValue");

        // Act
        await _stateMachine.SaveAsync(context);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<ConversationSession>(s =>
            s.CurrentState == ConversationState.MainMenu &&
            s.ContextJson != null &&
            s.ContextJson.Contains("testKey")
        )), Times.Once);
    }

    [Fact]
    public async Task ResetAsync_ResetsSessionToIdle()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.CartReview,
            ContextJson = "{\"cartItems\":[\"item1\"]}",
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        // Act
        await _stateMachine.ResetAsync(psid);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<ConversationSession>(s =>
            s.CurrentState == ConversationState.Idle &&
            s.ContextJson == null
        )), Times.Once);
    }

    // Note: ProcessMessageAsync integration with handlers is tested in IntegrationTests.ConversationFlowTests
    // Unit tests focus on state machine logic without handler dependencies

    [Fact]
    public async Task LoadOrCreateAsync_DeserializesContextJson()
    {
        // Arrange
        var psid = "test-psid-123";
        var contextData = new Dictionary<string, object>
        {
            { "cartItems", new List<string> { "item1", "item2" } },
            { "selectedProduct", "product-123" }
        };
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.CartReview,
            ContextJson = JsonSerializer.Serialize(contextData),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(55)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.NotNull(context.Data);
        Assert.True(context.Data.ContainsKey("cartItems"));
        Assert.True(context.Data.ContainsKey("selectedProduct"));
    }

    [Fact]
    public async Task LoadOrCreateAsync_HandlesInvalidJson_ReturnsEmptyData()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.MainMenu,
            ContextJson = "invalid-json{{{",
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(55)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);

        // Act
        var context = await _stateMachine.LoadOrCreateAsync(psid);

        // Assert
        Assert.NotNull(context.Data);
        Assert.Empty(context.Data);
    }

    [Fact]
    public async Task LoadOrCreateAsync_HydratesRememberedContact_WhenContextIsEmpty()
    {
        var psid = "returning-psid";
        var session = new ConversationSession
        {
            Id = "session-remembered-1",
            FacebookPSID = psid,
            FacebookPageId = "PAGE_1",
            CurrentState = ConversationState.Idle,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(58)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);
        _mockCustomerIntelligenceService
            .Setup(x => x.GetExistingAsync(psid, "PAGE_1", default))
            .ReturnsAsync(new CustomerIdentity
            {
                FacebookPSID = psid,
                FacebookPageId = "PAGE_1",
                PhoneNumber = "0901234567",
                ShippingAddress = "12 Tran Hung Dao",
                FullName = "Khach cu"
            });

        var context = await _stateMachine.LoadOrCreateAsync(psid, "PAGE_1");

        Assert.Equal("0901234567", context.GetData<string>("customerPhone"));
        Assert.Equal("12 Tran Hung Dao", context.GetData<string>("shippingAddress"));
        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("customer-identity", context.GetData<string>("contactMemorySource"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));
    }

    [Fact]
    public async Task LoadOrCreateAsync_DoesNotOverwriteExistingContact_WhenSessionAlreadyHasValues()
    {
        var psid = "returning-psid-2";
        var session = new ConversationSession
        {
            Id = "session-remembered-2",
            FacebookPSID = psid,
            FacebookPageId = "PAGE_1",
            CurrentState = ConversationState.CollectingInfo,
            ContextJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["customerPhone"] = "0999999999",
                ["shippingAddress"] = "99 Le Loi"
            }),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(58)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);
        _mockCustomerIntelligenceService
            .Setup(x => x.GetExistingAsync(psid, "PAGE_1", default))
            .ReturnsAsync(new CustomerIdentity
            {
                FacebookPSID = psid,
                FacebookPageId = "PAGE_1",
                PhoneNumber = "0901234567",
                ShippingAddress = "12 Tran Hung Dao"
            });

        var context = await _stateMachine.LoadOrCreateAsync(psid, "PAGE_1");

        Assert.Equal("0999999999", context.GetData<string>("customerPhone"));
        Assert.Equal("99 Le Loi", context.GetData<string>("shippingAddress"));
        Assert.Null(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Fact]
    public async Task LoadOrCreateAsync_RestoresRememberedContact_AfterTimeoutReset()
    {
        var psid = "returning-timeout-psid";
        var session = new ConversationSession
        {
            Id = "session-timeout-1",
            FacebookPSID = psid,
            FacebookPageId = "PAGE_1",
            CurrentState = ConversationState.CollectingInfo,
            ContextJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["selectedProductCodes"] = new List<string> { "KCN" }
            }),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-20),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(40)
        };
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid)).ReturnsAsync(session);
        _mockCustomerIntelligenceService
            .Setup(x => x.GetExistingAsync(psid, "PAGE_1", default))
            .ReturnsAsync(new CustomerIdentity
            {
                FacebookPSID = psid,
                FacebookPageId = "PAGE_1",
                PhoneNumber = "0901234567",
                ShippingAddress = "12 Tran Hung Dao"
            });

        var context = await _stateMachine.LoadOrCreateAsync(psid, "PAGE_1");

        Assert.Equal(ConversationState.Idle, context.CurrentState);
        Assert.Equal("0901234567", context.GetData<string>("customerPhone"));
        Assert.Equal("12 Tran Hung Dao", context.GetData<string>("shippingAddress"));
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<ConversationSession>(s =>
            s.CurrentState == ConversationState.Idle &&
            s.ContextJson == null)), Times.Once);
    }
}
