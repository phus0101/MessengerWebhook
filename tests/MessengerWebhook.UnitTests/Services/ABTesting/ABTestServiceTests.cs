using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.ABTesting.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.ABTesting;

public class ABTestServiceTests
{
    private MessengerBotDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new MessengerBotDbContext(options);
    }

    [Fact]
    public async Task GetVariantAsync_SamePSID_ReturnsSameVariant()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "test-seed-123"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        var psid = "test-psid-12345";
        var sessionId = Guid.NewGuid().ToString();

        // Create session in database
        var session = new ConversationSession
        {
            Id = sessionId,
            FacebookPSID = psid,
            TenantId = Guid.NewGuid()
        };
        dbContext.ConversationSessions.Add(session);
        await dbContext.SaveChangesAsync();

        // Clear change tracker to avoid tracking conflicts
        dbContext.ChangeTracker.Clear();

        // Act - Call multiple times with same PSID
        var variant1 = await service.GetVariantAsync(psid, sessionId);

        // Clear change tracker between calls
        dbContext.ChangeTracker.Clear();

        var variant2 = await service.GetVariantAsync(psid, sessionId);

        // Clear change tracker between calls
        dbContext.ChangeTracker.Clear();

        var variant3 = await service.GetVariantAsync(psid, sessionId);

        // Assert - All calls return same variant
        variant1.Should().Be(variant2);
        variant2.Should().Be(variant3);
        variant1.Should().BeOneOf("treatment", "control");
    }

    [Fact]
    public async Task GetVariantAsync_10KPSIDs_Distributes50_50()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "distribution-test"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        const int sampleSize = 10000;
        var treatmentCount = 0;
        var controlCount = 0;

        // Act - Assign variants to 10K unique PSIDs
        for (int i = 0; i < sampleSize; i++)
        {
            var psid = $"psid-{i:D6}";
            var sessionId = Guid.NewGuid().ToString();

            // Create session for each PSID
            var session = new ConversationSession
            {
                Id = sessionId,
                FacebookPSID = psid,
                TenantId = Guid.NewGuid()
            };
            dbContext.ConversationSessions.Add(session);
            await dbContext.SaveChangesAsync();

            // Clear change tracker to avoid tracking conflicts
            dbContext.ChangeTracker.Clear();

            var variant = await service.GetVariantAsync(psid, sessionId);

            if (variant == "treatment")
                treatmentCount++;
            else if (variant == "control")
                controlCount++;
        }

        // Assert - Distribution should be close to 50/50
        var treatmentPercentage = (treatmentCount / (double)sampleSize) * 100;
        var controlPercentage = (controlCount / (double)sampleSize) * 100;

        // Allow 2% deviation (48-52% range)
        treatmentPercentage.Should().BeInRange(48, 52);
        controlPercentage.Should().BeInRange(48, 52);
        (treatmentCount + controlCount).Should().Be(sampleSize);

        // Chi-square test for goodness of fit
        // H0: Distribution is 50/50
        // Expected: 5000 treatment, 5000 control
        var expected = sampleSize / 2.0;
        var chiSquare = Math.Pow(treatmentCount - expected, 2) / expected +
                       Math.Pow(controlCount - expected, 2) / expected;

        // Critical value for chi-square with df=1 at p=0.05 is 3.841
        // If chi-square < 3.841, we fail to reject H0 (distribution is good)
        chiSquare.Should().BeLessThan(3.841,
            $"Chi-square test failed: χ² = {chiSquare:F2} (treatment: {treatmentCount}, control: {controlCount})");
    }

    [Fact]
    public async Task GetVariantAsync_FeatureFlagDisabled_ReturnsTreatment()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = false, // Feature flag disabled
            TreatmentPercentage = 50,
            HashSeed = "test-seed"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);

        // Act - Test multiple PSIDs
        var results = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            var psid = $"psid-{i}";
            var sessionId = Guid.NewGuid().ToString();
            var variant = await service.GetVariantAsync(psid, sessionId);
            results.Add(variant);
        }

        // Assert - All users get treatment when disabled
        results.Should().AllBe("treatment");
        service.IsEnabled().Should().BeFalse();
    }

    [Fact]
    public void ValidateABTestingOptions_InvalidConfig_ThrowsException()
    {
        // Arrange
        var validator = new ValidateABTestingOptions();

        // Act & Assert - Test invalid TreatmentPercentage (negative)
        var invalidNegative = new ABTestingOptions
        {
            TreatmentPercentage = -10,
            HashSeed = "valid-seed"
        };
        var resultNegative = validator.Validate(null, invalidNegative);
        resultNegative.Failed.Should().BeTrue();
        resultNegative.FailureMessage.Should().Contain("TreatmentPercentage must be between 0 and 100");

        // Act & Assert - Test invalid TreatmentPercentage (over 100)
        var invalidOver100 = new ABTestingOptions
        {
            TreatmentPercentage = 150,
            HashSeed = "valid-seed"
        };
        var resultOver100 = validator.Validate(null, invalidOver100);
        resultOver100.Failed.Should().BeTrue();
        resultOver100.FailureMessage.Should().Contain("TreatmentPercentage must be between 0 and 100");

        // Act & Assert - Test empty HashSeed
        var invalidHashSeed = new ABTestingOptions
        {
            TreatmentPercentage = 50,
            HashSeed = ""
        };
        var resultHashSeed = validator.Validate(null, invalidHashSeed);
        resultHashSeed.Failed.Should().BeTrue();
        resultHashSeed.FailureMessage.Should().Contain("HashSeed cannot be empty");

        // Act & Assert - Test null HashSeed
        var invalidNullHashSeed = new ABTestingOptions
        {
            TreatmentPercentage = 50,
            HashSeed = null!
        };
        var resultNullHashSeed = validator.Validate(null, invalidNullHashSeed);
        resultNullHashSeed.Failed.Should().BeTrue();
        resultNullHashSeed.FailureMessage.Should().Contain("HashSeed cannot be empty");

        // Act & Assert - Test valid configuration
        var validOptions = new ABTestingOptions
        {
            TreatmentPercentage = 50,
            HashSeed = "valid-seed"
        };
        var resultValid = validator.Validate(null, validOptions);
        resultValid.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetVariantAsync_CachedVariant_ReturnsFromCache()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "cache-test"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        var psid = "cached-psid";
        var sessionId = Guid.NewGuid().ToString();

        // Create session with pre-assigned variant
        var session = new ConversationSession
        {
            Id = sessionId,
            FacebookPSID = psid,
            TenantId = Guid.NewGuid(),
            ABTestVariant = "treatment" // Pre-cached variant
        };
        dbContext.ConversationSessions.Add(session);
        await dbContext.SaveChangesAsync();

        // Act
        var variant = await service.GetVariantAsync(psid, sessionId);

        // Assert - Should return cached variant
        variant.Should().Be("treatment");

        // Verify debug log was called for cached variant
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("A/B variant cached")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetVariantAsync_DifferentHashSeeds_ProducesDifferentDistributions()
    {
        // Arrange
        using var dbContext1 = CreateDbContext();
        using var dbContext2 = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();

        var options1 = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "seed-1"
        });

        var options2 = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = 50,
            HashSeed = "seed-2"
        });

        var service1 = new ABTestService(dbContext1, options1, loggerMock.Object);
        var service2 = new ABTestService(dbContext2, options2, loggerMock.Object);

        // Act - Test same PSIDs with different seeds
        var differentAssignments = 0;
        const int testSize = 100;

        for (int i = 0; i < testSize; i++)
        {
            var psid = $"psid-{i}";
            var sessionId1 = Guid.NewGuid().ToString();
            var sessionId2 = Guid.NewGuid().ToString();

            var session1 = new ConversationSession
            {
                Id = sessionId1,
                FacebookPSID = psid,
                TenantId = Guid.NewGuid()
            };
            var session2 = new ConversationSession
            {
                Id = sessionId2,
                FacebookPSID = psid,
                TenantId = Guid.NewGuid()
            };
            dbContext1.ConversationSessions.Add(session1);
            dbContext2.ConversationSessions.Add(session2);
            await dbContext1.SaveChangesAsync();
            await dbContext2.SaveChangesAsync();

            // Clear change trackers to avoid tracking conflicts
            dbContext1.ChangeTracker.Clear();
            dbContext2.ChangeTracker.Clear();

            var variant1 = await service1.GetVariantAsync(psid, sessionId1);
            var variant2 = await service2.GetVariantAsync(psid, sessionId2);

            if (variant1 != variant2)
                differentAssignments++;
        }

        // Assert - Different seeds should produce different assignments
        // Expect roughly 50% different (not all same, not all different)
        differentAssignments.Should().BeGreaterThan(20,
            "Different hash seeds should produce different variant assignments");
    }

    [Theory]
    [InlineData(0)]    // 0% treatment = all control
    [InlineData(100)]  // 100% treatment = all treatment
    [InlineData(25)]   // 25% treatment, 75% control
    [InlineData(75)]   // 75% treatment, 25% control
    public async Task GetVariantAsync_CustomPercentages_RespectsConfiguration(int treatmentPercentage)
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<ABTestService>>();
        var options = Options.Create(new ABTestingOptions
        {
            Enabled = true,
            TreatmentPercentage = treatmentPercentage,
            HashSeed = $"test-{treatmentPercentage}"
        });

        var service = new ABTestService(dbContext, options, loggerMock.Object);
        const int sampleSize = 1000;
        var treatmentCount = 0;

        // Act
        for (int i = 0; i < sampleSize; i++)
        {
            var psid = $"psid-{treatmentPercentage}-{i}";
            var sessionId = Guid.NewGuid().ToString();

            var session = new ConversationSession
            {
                Id = sessionId,
                FacebookPSID = psid,
                TenantId = Guid.NewGuid()
            };
            dbContext.ConversationSessions.Add(session);
            await dbContext.SaveChangesAsync();

            // Clear change tracker to avoid tracking conflicts
            dbContext.ChangeTracker.Clear();

            var variant = await service.GetVariantAsync(psid, sessionId);
            if (variant == "treatment")
                treatmentCount++;
        }

        // Assert - Allow 5% deviation for smaller sample size
        var actualTreatmentPercentage = (treatmentCount / (double)sampleSize) * 100;
        actualTreatmentPercentage.Should().BeInRange(
            treatmentPercentage - 5,
            treatmentPercentage + 5,
            $"Expected ~{treatmentPercentage}% treatment, got {actualTreatmentPercentage:F1}%");
    }
}
