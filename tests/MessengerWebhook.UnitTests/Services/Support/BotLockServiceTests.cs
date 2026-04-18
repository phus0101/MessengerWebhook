using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Support;

public class BotLockServiceTests : IDisposable
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantContext;
    private readonly Mock<IAdminAuditService> _auditService;
    private readonly BotLockService _service;

    // Test DbContext that ignores ProductEmbedding (requires pgvector, not supported by InMemory)
    private class TestDbContext : MessengerBotDbContext
    {
        public TestDbContext(DbContextOptions<MessengerBotDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<ProductEmbedding>();
        }
    }

    public BotLockServiceTests()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);

        _tenantContext = new Mock<ITenantContext>();
        _tenantContext.Setup(x => x.TenantId).Returns(Guid.NewGuid());
        _tenantContext.Setup(x => x.FacebookPageId).Returns("page123");

        _auditService = new Mock<IAdminAuditService>();

        var supportOptions = Options.Create(new SupportOptions
        {
            BotLockTimeoutMinutes = 60
        });

        var logger = new Mock<ILogger<BotLockService>>();

        _service = new BotLockService(_dbContext, _tenantContext.Object, _auditService.Object, supportOptions, logger.Object);
    }

    [Fact]
    public async Task IsLockedAsync_NoLock_ReturnsFalse()
    {
        var result = await _service.IsLockedAsync("user123");

        Assert.False(result);
    }

    [Fact]
    public async Task IsLockedAsync_WithActiveLock_ReturnsTrue()
    {
        await _service.LockAsync("user123", "Test lock");

        var result = await _service.IsLockedAsync("user123");

        Assert.True(result);
    }

    [Fact]
    public async Task LockAsync_CreatesNewLock()
    {
        await _service.LockAsync("user123", "Test lock", Guid.NewGuid());

        var lock_ = await _dbContext.BotConversationLocks
            .FirstOrDefaultAsync(x => x.FacebookPSID == "user123");

        Assert.NotNull(lock_);
        Assert.True(lock_.IsLocked);
        Assert.Equal("Test lock", lock_.Reason);
    }

    [Fact]
    public async Task LockAsync_UpdatesExistingLock()
    {
        var caseId1 = Guid.NewGuid();
        var caseId2 = Guid.NewGuid();

        await _service.LockAsync("user123", "First lock", caseId1);
        await _service.LockAsync("user123", "Updated lock", caseId2);

        var locks = await _dbContext.BotConversationLocks
            .Where(x => x.FacebookPSID == "user123")
            .ToListAsync();

        Assert.Single(locks);
        Assert.Equal("Updated lock", locks[0].Reason);
        Assert.Equal(caseId2, locks[0].HumanSupportCaseId);
    }

    [Fact]
    public async Task ReleaseAsync_UnlocksActiveLock()
    {
        await _service.LockAsync("user123", "Test lock");

        await _service.ReleaseAsync("user123");

        var result = await _service.IsLockedAsync("user123");
        Assert.False(result);

        var lock_ = await _dbContext.BotConversationLocks
            .FirstOrDefaultAsync(x => x.FacebookPSID == "user123");
        Assert.NotNull(lock_);
        Assert.False(lock_.IsLocked);
        Assert.NotNull(lock_.ReleasedAt);
    }

    [Fact]
    public async Task ExtendLockAsync_ExtendsTimeout()
    {
        await _service.LockAsync("user123", "Test lock");

        var lockBefore = await _dbContext.BotConversationLocks
            .FirstAsync(x => x.FacebookPSID == "user123");
        var unlockAtBefore = lockBefore.UnlockAt;

        await _service.ExtendLockAsync("user123", 30);

        var lockAfter = await _dbContext.BotConversationLocks
            .FirstAsync(x => x.FacebookPSID == "user123");

        Assert.NotNull(lockAfter.UnlockAt);
        Assert.True(lockAfter.UnlockAt > unlockAtBefore);
    }

    [Fact]
    public async Task ExtendLockAsync_NoActiveLock_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ExtendLockAsync("user123", 30));
    }

    [Fact]
    public async Task GetActiveLocksAsync_ReturnsOnlyActiveLocks()
    {
        await _service.LockAsync("user1", "Lock 1");
        await _service.LockAsync("user2", "Lock 2");
        await _service.LockAsync("user3", "Lock 3");
        await _service.ReleaseAsync("user2");

        var activeLocks = await _service.GetActiveLocksAsync();

        Assert.Equal(2, activeLocks.Count);
        Assert.Contains(activeLocks, l => l.FacebookPSID == "user1");
        Assert.Contains(activeLocks, l => l.FacebookPSID == "user3");
        Assert.DoesNotContain(activeLocks, l => l.FacebookPSID == "user2");
    }

    [Fact]
    public async Task GetLockHistoryAsync_ReturnsAllLocksForUser()
    {
        await _service.LockAsync("user123", "Lock 1");
        await _service.ReleaseAsync("user123");
        await _service.LockAsync("user123", "Lock 2");

        var history = await _service.GetLockHistoryAsync("user123");

        Assert.Equal(2, history.Count);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
