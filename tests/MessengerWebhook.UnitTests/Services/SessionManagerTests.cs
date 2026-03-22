using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.Services;

public class SessionManagerTests
{
    private readonly Mock<ISessionRepository> _mockRepository;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<SessionManager>> _mockLogger;
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        _mockRepository = new Mock<ISessionRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        _mockLogger = new Mock<ILogger<SessionManager>>();
        _sessionManager = new SessionManager(_mockRepository.Object, _cache, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsSessionFromCache()
    {
        // Arrange
        var psid = "test-psid-123";
        var session = new ConversationSession
        {
            Id = "session-1",
            FacebookPSID = psid,
            CurrentState = ConversationState.MainMenu
        };

        // Pre-populate cache
        var cacheKey = $"session:{psid}";
        _cache.Set(cacheKey, session, new MemoryCacheEntryOptions().SetSize(1));

        // Act
        var result = await _sessionManager.GetAsync(psid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(psid, result.FacebookPSID);
        Assert.Equal(ConversationState.MainMenu, result.CurrentState);
        _mockRepository.Verify(r => r.GetByPSIDAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_FetchesFromDatabaseAndCaches()
    {
        // Arrange
        var psid = "test-psid-456";
        var session = new ConversationSession
        {
            Id = "session-2",
            FacebookPSID = psid,
            CurrentState = ConversationState.Greeting
        };

        _mockRepository.Setup(r => r.GetByPSIDAsync(psid))
            .ReturnsAsync(session);

        // Act
        var result = await _sessionManager.GetAsync(psid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(psid, result.FacebookPSID);
        _mockRepository.Verify(r => r.GetByPSIDAsync(psid), Times.Once);

        // Verify cache was updated
        var cacheKey = $"session:{psid}";
        var cachedSession = _cache.Get<ConversationSession>(cacheKey);
        Assert.NotNull(cachedSession);
        Assert.Equal(psid, cachedSession.FacebookPSID);
    }

    [Fact]
    public async Task GetAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var psid = "non-existent-psid";
        _mockRepository.Setup(r => r.GetByPSIDAsync(psid))
            .ReturnsAsync((ConversationSession?)null);

        // Act
        var result = await _sessionManager.GetAsync(psid);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByPSIDAsync(psid), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_UpdatesDatabaseAndCache()
    {
        // Arrange
        var session = new ConversationSession
        {
            Id = "session-3",
            FacebookPSID = "test-psid-789",
            CurrentState = ConversationState.ProductDetail
        };

        _mockRepository.Setup(r => r.UpdateAsync(session))
            .Returns(Task.CompletedTask);

        // Act
        await _sessionManager.SaveAsync(session);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(session), Times.Once);

        // Verify cache was updated
        var cacheKey = $"session:{session.FacebookPSID}";
        var cachedSession = _cache.Get<ConversationSession>(cacheKey);
        Assert.NotNull(cachedSession);
        Assert.Equal(session.FacebookPSID, cachedSession.FacebookPSID);
        Assert.Equal(ConversationState.ProductDetail, cachedSession.CurrentState);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingCache()
    {
        // Arrange
        var psid = "test-psid-update";
        var oldSession = new ConversationSession
        {
            Id = "session-4",
            FacebookPSID = psid,
            CurrentState = ConversationState.Idle
        };

        var newSession = new ConversationSession
        {
            Id = "session-4",
            FacebookPSID = psid,
            CurrentState = ConversationState.CartReview
        };

        // Pre-populate cache with old session
        var cacheKey = $"session:{psid}";
        _cache.Set(cacheKey, oldSession, new MemoryCacheEntryOptions().SetSize(1));

        _mockRepository.Setup(r => r.UpdateAsync(newSession))
            .Returns(Task.CompletedTask);

        // Act
        await _sessionManager.SaveAsync(newSession);

        // Assert
        var cachedSession = _cache.Get<ConversationSession>(cacheKey);
        Assert.NotNull(cachedSession);
        Assert.Equal(ConversationState.CartReview, cachedSession.CurrentState);
    }

    [Fact]
    public async Task DeleteAsync_EvictsFromCache()
    {
        // Arrange
        var psid = "test-psid-delete";
        var session = new ConversationSession
        {
            Id = "session-5",
            FacebookPSID = psid,
            CurrentState = ConversationState.MainMenu
        };

        // Pre-populate cache
        var cacheKey = $"session:{psid}";
        _cache.Set(cacheKey, session, new MemoryCacheEntryOptions().SetSize(1));

        // Act
        await _sessionManager.DeleteAsync(psid);

        // Assert
        var cachedSession = _cache.Get<ConversationSession>(cacheKey);
        Assert.Null(cachedSession);
    }

    [Fact]
    public async Task GetAsync_MultipleCalls_UsesCacheAfterFirstFetch()
    {
        // Arrange
        var psid = "test-psid-multiple";
        var session = new ConversationSession
        {
            Id = "session-6",
            FacebookPSID = psid,
            CurrentState = ConversationState.SkinAnalysis
        };

        _mockRepository.Setup(r => r.GetByPSIDAsync(psid))
            .ReturnsAsync(session);

        // Act
        var result1 = await _sessionManager.GetAsync(psid);
        var result2 = await _sessionManager.GetAsync(psid);
        var result3 = await _sessionManager.GetAsync(psid);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        _mockRepository.Verify(r => r.GetByPSIDAsync(psid), Times.Once);
    }

    [Fact]
    public async Task GetAsync_AfterDelete_FetchesFromDatabaseAgain()
    {
        // Arrange
        var psid = "test-psid-delete-refetch";
        var session = new ConversationSession
        {
            Id = "session-7",
            FacebookPSID = psid,
            CurrentState = ConversationState.Help
        };

        _mockRepository.Setup(r => r.GetByPSIDAsync(psid))
            .ReturnsAsync(session);

        // Act
        var result1 = await _sessionManager.GetAsync(psid);
        await _sessionManager.DeleteAsync(psid);
        var result2 = await _sessionManager.GetAsync(psid);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        _mockRepository.Verify(r => r.GetByPSIDAsync(psid), Times.Exactly(2));
    }

    [Fact]
    public async Task SaveAsync_ConcurrentCalls_HandlesGracefully()
    {
        // Arrange
        var psid = "test-psid-concurrent";
        var sessions = Enumerable.Range(1, 10).Select(i => new ConversationSession
        {
            Id = $"session-{i}",
            FacebookPSID = psid,
            CurrentState = ConversationState.MainMenu
        }).ToList();

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ConversationSession>()))
            .Returns(Task.CompletedTask);

        // Act
        var tasks = sessions.Select(s => _sessionManager.SaveAsync(s));
        await Task.WhenAll(tasks);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ConversationSession>()), Times.Exactly(10));

        // Verify cache contains last saved session
        var cacheKey = $"session:{psid}";
        var cachedSession = _cache.Get<ConversationSession>(cacheKey);
        Assert.NotNull(cachedSession);
    }

    [Fact]
    public async Task GetAsync_ConcurrentCalls_FetchesOnlyOnce()
    {
        // Arrange
        var psid = "test-psid-concurrent-get";
        var session = new ConversationSession
        {
            Id = "session-8",
            FacebookPSID = psid,
            CurrentState = ConversationState.OrderTracking
        };

        _mockRepository.Setup(r => r.GetByPSIDAsync(psid))
            .ReturnsAsync(session);

        // Act - First call to populate cache
        await _sessionManager.GetAsync(psid);

        // Multiple concurrent calls
        var tasks = Enumerable.Range(1, 10).Select(_ => _sessionManager.GetAsync(psid));
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        _mockRepository.Verify(r => r.GetByPSIDAsync(psid), Times.Once);
    }
}
