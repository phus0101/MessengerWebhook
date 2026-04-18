using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Emotion.Configuration;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.Tone.Configuration;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Conversation.Configuration;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.SmallTalk.Configuration;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Configuration;
using MessengerWebhook.Services.ResponseValidation.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;
using Xunit;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.IntegrationTests.Services;

/// <summary>
/// Performance benchmark tests for naturalness pipeline services.
/// Verifies that all services meet latency targets and cache performance goals.
/// </summary>
public class NaturalnessPerformanceBenchmarkTests
{
    private readonly IEmotionDetectionService _emotionService;
    private readonly IToneMatchingService _toneService;
    private readonly IConversationContextAnalyzer _contextAnalyzer;
    private readonly ISmallTalkService _smallTalkService;
    private readonly IResponseValidationService _validationService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ITenantContext> _mockTenantContext;

    public NaturalnessPerformanceBenchmarkTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        _emotionService = new EmotionDetectionService(
            _cache,
            _mockTenantContext.Object,
            NullLogger<EmotionDetectionService>.Instance,
            Options.Create(new EmotionDetectionOptions()));

        _toneService = new ToneMatchingService(
            _cache,
            _mockTenantContext.Object,
            NullLogger<ToneMatchingService>.Instance,
            Options.Create(new ToneMatchingOptions()));

        var patternDetector = new PatternDetector();
        var topicAnalyzer = new TopicAnalyzer();
        _contextAnalyzer = new ConversationContextAnalyzer(
            patternDetector,
            topicAnalyzer,
            _cache,
            _mockTenantContext.Object,
            NullLogger<ConversationContextAnalyzer>.Instance,
            Options.Create(new ConversationAnalysisOptions()));

        var smallTalkDetector = new SmallTalkDetector();
        _smallTalkService = new SmallTalkService(
            smallTalkDetector,
            NullLogger<SmallTalkService>.Instance,
            Options.Create(new SmallTalkOptions()));

        _validationService = new ResponseValidationService(
            Options.Create(new ResponseValidationOptions()),
            NullLogger<ResponseValidationService>.Instance);
    }

    [Fact]
    public async Task Performance_EmotionDetection_ShouldBeUnder100ms()
    {
        // Arrange
        var message = "Chào shop, em muốn mua kem chống nắng";
        var history = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = message }
        };

        var iterations = 50;
        var latencies = new List<long>();

        // Act - Measure multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _emotionService.DetectEmotionWithContextAsync(message, history, CancellationToken.None);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        // Assert
        Assert.True(p95Latency < 100,
            $"Emotion detection P95 latency ({p95Latency}ms) exceeds target (100ms). Avg: {avgLatency:F1}ms");
    }

    [Fact]
    public async Task Performance_ContextAnalysis_ShouldBeUnder50msFor10Turns()
    {
        // Arrange - 10-turn conversation history
        var history = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Chào shop" },
            new() { Role = "assistant", Content = "Dạ chào chị" },
            new() { Role = "user", Content = "Shop có kem chống nắng không?" },
            new() { Role = "assistant", Content = "Dạ có ạ" },
            new() { Role = "user", Content = "Giá bao nhiêu?" },
            new() { Role = "assistant", Content = "320k ạ" },
            new() { Role = "user", Content = "Có ship không?" },
            new() { Role = "assistant", Content = "Dạ có freeship" },
            new() { Role = "user", Content = "Bao lâu nhận được?" },
            new() { Role = "assistant", Content = "2-3 ngày ạ" }
        };

        var emotion = new MessengerWebhook.Services.Emotion.Models.EmotionScore
        {
            PrimaryEmotion = MessengerWebhook.Services.Emotion.Models.EmotionType.Neutral,
            Confidence = 0.8
        };

        var iterations = 50;
        var latencies = new List<long>();

        // Act - Measure multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _contextAnalyzer.AnalyzeWithEmotionAsync(
                history,
                new List<MessengerWebhook.Services.Emotion.Models.EmotionScore> { emotion },
                CancellationToken.None);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        // Assert
        Assert.True(p95Latency < 50,
            $"Context analysis P95 latency ({p95Latency}ms) exceeds target (50ms) for 10 turns. Avg: {avgLatency:F1}ms");
    }

    [Fact]
    public async Task Performance_ToneMatching_ShouldBeUnder50ms()
    {
        // Arrange
        var emotion = new MessengerWebhook.Services.Emotion.Models.EmotionScore
        {
            PrimaryEmotion = MessengerWebhook.Services.Emotion.Models.EmotionType.Neutral,
            Confidence = 0.8
        };

        var vipProfile = new VipProfile
        {
            Tier = VipTier.Standard,
            GreetingStyle = "formal"
        };

        var customer = new CustomerIdentity
        {
            TotalOrders = 2,
            LifetimeValue = 700000
        };

        var iterations = 50;
        var latencies = new List<long>();

        // Act - Measure multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _toneService.GenerateToneProfileAsync(
                emotion, vipProfile, customer, 1, CancellationToken.None);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        // Assert
        Assert.True(p95Latency < 50,
            $"Tone matching P95 latency ({p95Latency}ms) exceeds target (50ms). Avg: {avgLatency:F1}ms");
    }

    [Fact]
    public async Task Performance_SmallTalk_ShouldBeUnder30ms()
    {
        // Arrange
        var message = "Chào shop";
        var emotion = new MessengerWebhook.Services.Emotion.Models.EmotionScore
        {
            PrimaryEmotion = MessengerWebhook.Services.Emotion.Models.EmotionType.Neutral,
            Confidence = 0.8
        };

        var toneProfile = new MessengerWebhook.Services.Tone.Models.ToneProfile
        {
            Level = MessengerWebhook.Services.Tone.Models.ToneLevel.Formal,
            PronounText = "chị/em"
        };

        var context = new MessengerWebhook.Services.Conversation.Models.ConversationContext
        {
            CurrentStage = MessengerWebhook.Services.Conversation.Models.JourneyStage.Browsing
        };

        var vipProfile = new VipProfile
        {
            Tier = VipTier.Standard,
            GreetingStyle = "formal"
        };

        var iterations = 50;
        var latencies = new List<long>();

        // Act - Measure multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _smallTalkService.AnalyzeAsync(
                message, emotion, toneProfile, context, vipProfile, false, 1, CancellationToken.None);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        // Assert
        Assert.True(p95Latency < 30,
            $"Small talk P95 latency ({p95Latency}ms) exceeds target (30ms). Avg: {avgLatency:F1}ms");
    }

    [Fact]
    public async Task Performance_Validation_ShouldBeUnder50ms()
    {
        // Arrange
        var response = "Dạ chào chị ạ! Em có thể tư vấn gì cho chị không ạ?";
        var toneProfile = new MessengerWebhook.Services.Tone.Models.ToneProfile
        {
            Level = MessengerWebhook.Services.Tone.Models.ToneLevel.Formal,
            PronounText = "chị/em"
        };

        var context = new MessengerWebhook.Services.Conversation.Models.ConversationContext
        {
            CurrentStage = MessengerWebhook.Services.Conversation.Models.JourneyStage.Browsing
        };

        var smallTalkResponse = new MessengerWebhook.Services.SmallTalk.Models.SmallTalkResponse
        {
            IsSmallTalk = true,
            Intent = MessengerWebhook.Services.SmallTalk.Models.SmallTalkIntent.Greeting
        };

        var validationContext = new ResponseValidationContext
        {
            Response = response,
            ToneProfile = toneProfile,
            ConversationContext = context,
            SmallTalkResponse = smallTalkResponse
        };

        var iterations = 50;
        var latencies = new List<long>();

        // Act - Measure multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _validationService.ValidateAsync(validationContext, CancellationToken.None);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        // Assert
        Assert.True(p95Latency < 50,
            $"Validation P95 latency ({p95Latency}ms) exceeds target (50ms). Avg: {avgLatency:F1}ms");
    }

    [Fact]
    public async Task Performance_FullPipeline_ShouldBeUnder100msTotal()
    {
        // Arrange
        var message = "Chào shop, em muốn mua kem chống nắng";
        var history = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = message }
        };

        var customer = new CustomerIdentity { TotalOrders = 1 };
        var vipProfile = new VipProfile { Tier = VipTier.Standard, GreetingStyle = "formal" };

        var iterations = 50;
        var latencies = new List<long>();

        // Act - Measure full pipeline
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();

            // Step 1: Emotion Detection
            var emotion = await _emotionService.DetectEmotionWithContextAsync(
                message, history, CancellationToken.None);

            // Step 2: Context Analysis
            var context = await _contextAnalyzer.AnalyzeWithEmotionAsync(
                history,
                new List<MessengerWebhook.Services.Emotion.Models.EmotionScore> { emotion },
                CancellationToken.None);

            // Step 3: Tone Matching
            var toneProfile = await _toneService.GenerateToneProfileAsync(
                emotion, vipProfile, customer, 1, CancellationToken.None);

            // Step 4: Small Talk
            var smallTalkResponse = await _smallTalkService.AnalyzeAsync(
                message, emotion, toneProfile, context, vipProfile, false, 1, CancellationToken.None);

            // Step 5: Validation
            var validationContext = new ResponseValidationContext
            {
                Response = "Dạ chào chị ạ!",
                ToneProfile = toneProfile,
                ConversationContext = context,
                SmallTalkResponse = smallTalkResponse
            };
            await _validationService.ValidateAsync(validationContext, CancellationToken.None);

            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        // Assert
        Assert.True(p95Latency < 100,
            $"Full pipeline P95 latency ({p95Latency}ms) exceeds target (100ms). Avg: {avgLatency:F1}ms");
    }

    [Fact]
    public async Task Performance_CacheHit_ShouldBeSignificantlyFaster()
    {
        // Arrange
        var message = "Chào shop";
        var history = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = message }
        };

        var customer = new CustomerIdentity { TotalOrders = 1 };
        var vipProfile = new VipProfile { Tier = VipTier.Standard, GreetingStyle = "formal" };

        // Act - First call (cache miss)
        var sw1 = Stopwatch.StartNew();
        var emotion1 = await _emotionService.DetectEmotionWithContextAsync(
            message, history, CancellationToken.None);
        var tone1 = await _toneService.GenerateToneProfileAsync(
            emotion1, vipProfile, customer, 1, CancellationToken.None);
        sw1.Stop();
        var cacheMissLatency = sw1.ElapsedMilliseconds;

        // Act - Second call (cache hit)
        var sw2 = Stopwatch.StartNew();
        var emotion2 = await _emotionService.DetectEmotionWithContextAsync(
            message, history, CancellationToken.None);
        var tone2 = await _toneService.GenerateToneProfileAsync(
            emotion2, vipProfile, customer, 1, CancellationToken.None);
        sw2.Stop();
        var cacheHitLatency = sw2.ElapsedMilliseconds;

        // Assert - Cache hit should be faster (or both too fast to measure)
        if (cacheMissLatency > 0)
        {
            Assert.True(cacheHitLatency <= cacheMissLatency,
                $"Cache hit ({cacheHitLatency}ms) should be <= cache miss ({cacheMissLatency}ms)");
        }

        // If measurable, cache hit should be under 10ms
        if (cacheHitLatency > 0)
        {
            Assert.True(cacheHitLatency < 10,
                $"Cache hit latency ({cacheHitLatency}ms) exceeds target (10ms)");
        }
    }

    [Fact]
    public async Task Performance_LongHistory_ShouldScaleLinearly()
    {
        // Arrange - Test with different history lengths
        var emotion = new MessengerWebhook.Services.Emotion.Models.EmotionScore
        {
            PrimaryEmotion = MessengerWebhook.Services.Emotion.Models.EmotionType.Neutral,
            Confidence = 0.8
        };

        var historySizes = new[] { 1, 5, 10, 20 };
        var results = new Dictionary<int, long>();

        foreach (var size in historySizes)
        {
            var history = new List<AiConversationMessage>();
            for (int i = 0; i < size; i++)
            {
                history.Add(new AiConversationMessage
                {
                    Role = i % 2 == 0 ? "user" : "assistant",
                    Content = $"Message {i + 1}"
                });
            }

            // Measure 10 iterations
            var latencies = new List<long>();
            for (int i = 0; i < 10; i++)
            {
                var sw = Stopwatch.StartNew();
                await _contextAnalyzer.AnalyzeWithEmotionAsync(
                    history,
                    new List<MessengerWebhook.Services.Emotion.Models.EmotionScore> { emotion },
                    CancellationToken.None);
                sw.Stop();
                latencies.Add(sw.ElapsedMilliseconds);
            }

            results[size] = (long)latencies.Average();
        }

        // Assert - 10 turns should be under 50ms
        Assert.True(results[10] < 50,
            $"Context analysis for 10 turns ({results[10]}ms) exceeds target (50ms)");

        // Assert - 20 turns should be under 100ms
        Assert.True(results[20] < 100,
            $"Context analysis for 20 turns ({results[20]}ms) exceeds target (100ms)");

        // Assert - Should scale roughly linearly (allow for fast execution)
        // If both are 0ms, the test passes (execution too fast to measure)
        if (results[10] > 0 && results[20] > 0)
        {
            Assert.True(results[20] < results[10] * 3,
                $"Context analysis scaling is non-linear: 10 turns={results[10]}ms, 20 turns={results[20]}ms");
        }
    }
}
