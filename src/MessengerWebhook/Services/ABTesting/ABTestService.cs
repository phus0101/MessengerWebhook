using System.Security.Cryptography;
using System.Text;
using MessengerWebhook.Data;
using MessengerWebhook.Services.ABTesting.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.ABTesting;

public class ABTestService : IABTestService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ABTestingOptions _options;
    private readonly ILogger<ABTestService> _logger;

    public ABTestService(
        MessengerBotDbContext dbContext,
        IOptions<ABTestingOptions> options,
        ILogger<ABTestService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetVariantAsync(string psid, string sessionId, CancellationToken cancellationToken = default)
    {
        // If A/B testing disabled, everyone gets treatment (full pipeline)
        if (!_options.Enabled)
        {
            return "treatment";
        }

        var startTime = DateTime.UtcNow;

        // Check if variant already assigned to this session (with TenantId for isolation)
        // Remove AsNoTracking to allow EF Core to track changes
        var session = await _dbContext.ConversationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        // Tenant isolation: Query already filtered by global query filter in DbContext

        if (session?.ABTestVariant != null)
        {
            var cachedLatency = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogDebug("A/B variant cached for PSID {PSID}: {Variant} (latency: {Latency}ms)",
                psid, session.ABTestVariant, cachedLatency);
            return session.ABTestVariant;
        }

        // Assign variant deterministically based on PSID hash
        var variant = AssignVariant(psid);

        // Store variant in session
        if (session != null)
        {
            session.ABTestVariant = variant;
            // No need for .Update() - EF Core tracks changes automatically
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var totalLatency = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("A/B variant assigned for PSID {PSID}: {Variant} (latency: {Latency}ms)",
            psid, variant, totalLatency);

        return variant;
    }

    public bool IsEnabled()
    {
        return _options.Enabled;
    }

    /// <summary>
    /// Deterministic variant assignment using SHA256 hash.
    /// Same PSID always gets same variant.
    /// </summary>
    private string AssignVariant(string psid)
    {
        var input = $"{psid}:{_options.HashSeed}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var bucket = BitConverter.ToUInt32(hash, 0) % 100;

        return bucket < _options.TreatmentPercentage ? "treatment" : "control";
    }
}
