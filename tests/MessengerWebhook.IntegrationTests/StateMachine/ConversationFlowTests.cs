using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Services.AI;
using MessengerWebhook.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.IntegrationTests.StateMachine;

/// <summary>
/// Integration tests for conversation flow and state machine integration
/// Note: Some tests are limited by stub implementation in ConversationStateMachine.ProcessMessageAsync
/// Full state handler logic will be implemented in later phases
/// </summary>
public class ConversationFlowTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public ConversationFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessMessage_InitialGreeting_TransitionsFromIdleToGreeting()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var sessionRepo = new SessionRepository(context);
        var stateMachineLogger = Mock.Of<ILogger<ConversationStateMachine>>();
        var geminiService = Mock.Of<IGeminiService>();

        // Create state handlers
        var handlers = new List<IStateHandler>
        {
            new IdleStateHandler(
                geminiService,
                Mock.Of<ILogger<IdleStateHandler>>()),
            new GreetingStateHandler(
                geminiService,
                Mock.Of<ILogger<GreetingStateHandler>>())
        };

        // Create state machine
        var stateMachine = new ConversationStateMachine(sessionRepo, handlers, stateMachineLogger);

        var psid = $"test_user_{Guid.NewGuid():N}";

        // Act - Initial greeting (Idle -> Greeting)
        var reply = await stateMachine.ProcessMessageAsync(psid, "hello");

        // Assert
        reply.Should().NotBeNullOrEmpty();
        reply.Should().Contain("Welcome");

        var ctx = await stateMachine.LoadOrCreateAsync(psid);
        ctx.CurrentState.Should().Be(ConversationState.Greeting);

        // Verify session persisted
        var session = await sessionRepo.GetByPSIDAsync(psid);
        session.Should().NotBeNull();
        session!.CurrentState.Should().Be(ConversationState.Greeting);
        session.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StatePersistence_AcrossMultipleRequests_MaintainsContext()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var sessionRepo = new SessionRepository(context);
        var logger = Mock.Of<ILogger<ConversationStateMachine>>();
        var handlers = Enumerable.Empty<IStateHandler>();
        var stateMachine = new ConversationStateMachine(sessionRepo, handlers, logger);

        var psid = $"test_user_{Guid.NewGuid():N}";

        // Act - First request: greeting
        await stateMachine.ProcessMessageAsync(psid, "hi");
        var ctx1 = await stateMachine.LoadOrCreateAsync(psid);
        var state1 = ctx1.CurrentState;

        // Simulate new request - create new state machine instance
        var stateMachine2 = new ConversationStateMachine(sessionRepo, handlers, logger);
        var ctx2 = await stateMachine2.LoadOrCreateAsync(psid);

        // Assert - State should be preserved
        ctx2.CurrentState.Should().Be(state1);
        ctx2.SessionId.Should().Be(ctx1.SessionId);
        ctx2.FacebookPSID.Should().Be(psid);
    }

    [Fact]
    public async Task ErrorHandling_InvalidStateTransition_LogsWarning()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var sessionRepo = new SessionRepository(context);
        var mockLogger = new Mock<ILogger<ConversationStateMachine>>();
        var handlers = Enumerable.Empty<IStateHandler>();
        var stateMachine = new ConversationStateMachine(sessionRepo, handlers, mockLogger.Object);

        var psid = $"test_user_{Guid.NewGuid():N}";

        // Act - Create session in Idle state
        var ctx = await stateMachine.LoadOrCreateAsync(psid);
        ctx.CurrentState.Should().Be(ConversationState.Idle);

        // Try invalid transition (Idle -> ShippingAddress without going through cart flow)
        var result = await stateMachine.TransitionToAsync(ctx, ConversationState.ShippingAddress);

        // Assert - Transition should fail
        result.Should().BeFalse();
        ctx.CurrentState.Should().Be(ConversationState.Idle); // Should remain in Idle

        // Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid state transition")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupService_DeletesExpiredSessions_Successfully()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var sessionRepo = new SessionRepository(context);

        // Create expired session
        var expiredSession = new ConversationSession
        {
            FacebookPSID = $"expired_{Guid.NewGuid():N}",
            CurrentState = ConversationState.Idle,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastActivityAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired 5 minutes ago
        };
        await sessionRepo.CreateAsync(expiredSession);

        // Create active session
        var activeSession = new ConversationSession
        {
            FacebookPSID = $"active_{Guid.NewGuid():N}",
            CurrentState = ConversationState.Greeting,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1) // Expires in 1 hour
        };
        await sessionRepo.CreateAsync(activeSession);

        // Act - Run cleanup
        await sessionRepo.DeleteExpiredSessionsAsync();

        // Assert - Expired session should be deleted, active session should remain
        var expiredCheck = await sessionRepo.GetByPSIDAsync(expiredSession.FacebookPSID);
        expiredCheck.Should().BeNull();

        var activeCheck = await sessionRepo.GetByPSIDAsync(activeSession.FacebookPSID);
        activeCheck.Should().NotBeNull();
        activeCheck!.FacebookPSID.Should().Be(activeSession.FacebookPSID);
    }
}
