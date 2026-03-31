using System.Collections.Concurrent;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Nobita;
using MessengerWebhook.Services.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessengerWebhook.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"messenger-tests-{Guid.NewGuid():N}";

    public Guid PrimaryTenantId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid SecondaryTenantId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public string PrimaryPageId => "PAGE_TEST_1";
    public string PrimaryAltPageId => "PAGE_TEST_1_ALT";
    public string SecondaryPageId => "PAGE_TEST_2";
    public string PrimaryManagerEmail => "manager-primary@test.local";
    public string SecondaryManagerEmail => "manager-secondary@test.local";
    public string AdminPassword => "Password123!";
    public string AppSecret => "test_app_secret";
    public string VerifyToken => "test_verify_token_12345";

    public TestMessengerService MessengerSpy { get; } = new();
    public TestGeminiService GeminiStub { get; } = new();
    public TestEmbeddingService EmbeddingStub { get; } = new();
    public TestNobitaClient NobitaStub { get; } = new();
    public TestEmailNotificationService EmailSpy { get; } = new();

    protected virtual string HostEnvironment => "Testing";
    protected virtual IDictionary<string, string?> AdditionalConfiguration => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostEnvironment);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Facebook:AppSecret"] = AppSecret,
                ["Facebook:PageAccessToken"] = "test_page_access_token",
                ["Webhook:VerifyToken"] = VerifyToken,
                ["Gemini:ApiKey"] = "test_gemini_api_key",
                ["Admin:BootstrapEmail"] = PrimaryManagerEmail,
                ["Admin:BootstrapPassword"] = AdminPassword,
                ["Admin:BootstrapFullName"] = "Primary Manager",
                ["Email:FromAddress"] = "noreply@test.local",
                ["Email:SupportRecipient"] = PrimaryManagerEmail,
                ["Nobita:BaseUrl"] = "https://nobita.test.local/",
                ["Nobita:ApiKey"] = "test_nobita_api_key",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=ignored;Username=ignored;Password=ignored"
            };

            foreach (var entry in AdditionalConfiguration)
            {
                settings[entry.Key] = entry.Value;
            }

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<MessengerBotDbContext>();
            services.RemoveAll<DbContextOptions<MessengerBotDbContext>>();
            services.AddDbContext<MessengerBotDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IMessengerService>();
            services.RemoveAll<IGeminiService>();
            services.RemoveAll<IEmbeddingService>();
            services.RemoveAll<INobitaClient>();
            services.RemoveAll<IEmailNotificationService>();

            services.AddSingleton<IMessengerService>(MessengerSpy);
            services.AddSingleton<IGeminiService>(GeminiStub);
            services.AddSingleton<IEmbeddingService>(EmbeddingStub);
            services.AddSingleton<INobitaClient>(NobitaStub);
            services.AddSingleton<IEmailNotificationService>(EmailSpy);

            services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(options =>
            {
                var graphApiCheck = options.Registrations.FirstOrDefault(x => x.Name == "graph_api");
                if (graphApiCheck != null)
                {
                    options.Registrations.Remove(graphApiCheck);
                }
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            SeedDatabase(scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>());
        });
    }

    public async Task ResetStateAsync()
    {
        MessengerSpy.Clear();
        EmailSpy.Clear();
        NobitaStub.Reset();
        GeminiStub.Clear();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        dbContext.ChangeTracker.Clear();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        SeedDatabase(dbContext);
    }

    private void SeedDatabase(MessengerBotDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ManagerProfile>();

        var primaryPageConfigId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var primaryAltPageConfigId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var secondaryPageConfigId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var primaryManagerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var secondaryManagerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        dbContext.Tenants.AddRange(
            new Tenant
            {
                Id = PrimaryTenantId,
                Code = "mui-xu-primary",
                Name = "Mui Xu Primary"
            },
            new Tenant
            {
                Id = SecondaryTenantId,
                Code = "mui-xu-secondary",
                Name = "Mui Xu Secondary"
            });

        dbContext.FacebookPageConfigs.AddRange(
            new FacebookPageConfig
            {
                Id = primaryPageConfigId,
                TenantId = PrimaryTenantId,
                FacebookPageId = PrimaryPageId,
                PageName = "Mui Xu Page 1",
                DefaultManagerEmail = PrimaryManagerEmail,
                PageAccessToken = "page-token-1",
                VerifyToken = VerifyToken,
                IsPrimaryPage = true
            },
            new FacebookPageConfig
            {
                Id = primaryAltPageConfigId,
                TenantId = PrimaryTenantId,
                FacebookPageId = PrimaryAltPageId,
                PageName = "Mui Xu Page 1 Alt",
                DefaultManagerEmail = PrimaryManagerEmail,
                PageAccessToken = "page-token-1-alt",
                VerifyToken = VerifyToken,
                IsPrimaryPage = false
            },
            new FacebookPageConfig
            {
                Id = secondaryPageConfigId,
                TenantId = SecondaryTenantId,
                FacebookPageId = SecondaryPageId,
                PageName = "Mui Xu Page 2",
                DefaultManagerEmail = SecondaryManagerEmail,
                PageAccessToken = "page-token-2",
                VerifyToken = VerifyToken,
                IsPrimaryPage = true
            });

        var primaryManager = new ManagerProfile
        {
            Id = primaryManagerId,
            TenantId = PrimaryTenantId,
            FacebookPageConfigId = primaryPageConfigId,
            FullName = "Primary Manager",
            Email = PrimaryManagerEmail,
            IsPrimary = true,
            IsActive = true
        };
        primaryManager.PasswordHash = passwordHasher.HashPassword(primaryManager, AdminPassword);

        var secondaryManager = new ManagerProfile
        {
            Id = secondaryManagerId,
            TenantId = SecondaryTenantId,
            FacebookPageConfigId = secondaryPageConfigId,
            FullName = "Secondary Manager",
            Email = SecondaryManagerEmail,
            IsPrimary = true,
            IsActive = true
        };
        secondaryManager.PasswordHash = passwordHasher.HashPassword(secondaryManager, AdminPassword);

        dbContext.ManagerProfiles.AddRange(primaryManager, secondaryManager);

        SeedCatalog(dbContext);
        SeedDraftsAndCases(dbContext);

        dbContext.SaveChanges();
    }

    private void SeedCatalog(MessengerBotDbContext dbContext)
    {
        dbContext.Products.AddRange(
            new Product
            {
                Id = "product-kcn",
                TenantId = PrimaryTenantId,
                Code = "KCN",
                Name = "Kem Chong Nang",
                Description = "Kem chong nang ban chay",
                Brand = "Mui Xu",
                BasePrice = 320000m,
                NobitaProductId = 101,
                NobitaWeight = 0.25m
            },
            new Product
            {
                Id = "product-kl",
                TenantId = PrimaryTenantId,
                Code = "KL",
                Name = "Kem Lua",
                Description = "Kem lua duong sang",
                Brand = "Mui Xu",
                BasePrice = 410000m,
                NobitaProductId = 102,
                NobitaWeight = 0.20m
            },
            new Product
            {
                Id = "product-combo",
                TenantId = PrimaryTenantId,
                Code = "COMBO_2",
                Name = "Combo 2 San Pham",
                Description = "Combo 2 san pham freeship",
                Brand = "Mui Xu",
                BasePrice = 730000m,
                NobitaProductId = 103,
                NobitaWeight = 0.45m
            },
            new Product
            {
                Id = "product-other-tenant",
                TenantId = SecondaryTenantId,
                Code = "OTHER_1",
                Name = "Other Tenant Product",
                Description = "Hidden from primary manager",
                Brand = "Mui Xu",
                BasePrice = 999000m,
                NobitaProductId = 201,
                NobitaWeight = 0.30m
            });

        dbContext.Gifts.AddRange(
            new Gift { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), TenantId = PrimaryTenantId, Code = "GIFT_KCN", Name = "Mat na duong sang" },
            new Gift { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), TenantId = PrimaryTenantId, Code = "GIFT_KL", Name = "Tinh chat mini" },
            new Gift { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), TenantId = PrimaryTenantId, Code = "GIFT_COMBO", Name = "Set qua freeship" });

        dbContext.ProductGiftMappings.AddRange(
            new ProductGiftMapping { TenantId = PrimaryTenantId, ProductCode = "KCN", GiftCode = "GIFT_KCN", Priority = 10 },
            new ProductGiftMapping { TenantId = PrimaryTenantId, ProductCode = "KL", GiftCode = "GIFT_KL", Priority = 10 },
            new ProductGiftMapping { TenantId = PrimaryTenantId, ProductCode = "COMBO_2", GiftCode = "GIFT_COMBO", Priority = 10 });
    }

    private void SeedDraftsAndCases(MessengerBotDbContext dbContext)
    {
        var primaryCustomerId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var secondaryCustomerId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var primaryAltCustomerId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var primaryExistingCustomerId = Guid.Parse("30000000-0000-0000-0000-000000000004");

        dbContext.CustomerIdentities.AddRange(
            new CustomerIdentity
            {
                Id = primaryCustomerId,
                TenantId = PrimaryTenantId,
                FacebookPSID = "psid-primary",
                FacebookPageId = PrimaryPageId,
                PhoneNumber = "0900000001",
                FullName = "Khach Primary",
                ShippingAddress = "1 Nguyen Hue, Quan 1"
            },
            new CustomerIdentity
            {
                Id = primaryAltCustomerId,
                TenantId = PrimaryTenantId,
                FacebookPSID = "psid-primary-alt",
                FacebookPageId = PrimaryAltPageId,
                PhoneNumber = "0900000003",
                FullName = "Khach Primary Alt",
                ShippingAddress = "3 Hai Ba Trung, Quan 1"
            },
            new CustomerIdentity
            {
                Id = primaryExistingCustomerId,
                TenantId = PrimaryTenantId,
                FacebookPSID = "psid-primary-existing",
                FacebookPageId = PrimaryPageId,
                PhoneNumber = "0911111111",
                FullName = "Khach Gan Lai",
                ShippingAddress = "22 Ly Tu Trong, Quan 1",
                TotalOrders = 12,
                SuccessfulDeliveries = 11,
                FailedDeliveries = 1
            },
            new CustomerIdentity
            {
                Id = secondaryCustomerId,
                TenantId = SecondaryTenantId,
                FacebookPSID = "psid-secondary",
                FacebookPageId = SecondaryPageId,
                PhoneNumber = "0900000002",
                FullName = "Khach Secondary",
                ShippingAddress = "2 Le Loi, Quan 3"
            });

        var primaryDraftId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var secondaryDraftId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        var primaryAltDraftId = Guid.Parse("40000000-0000-0000-0000-000000000003");

        dbContext.DraftOrders.AddRange(
            new DraftOrder
            {
                Id = primaryDraftId,
                TenantId = PrimaryTenantId,
                DraftCode = "DR-PRIMARY-001",
                CustomerIdentityId = primaryCustomerId,
                FacebookPSID = "psid-primary",
                FacebookPageId = PrimaryPageId,
                CustomerName = "Khach Primary",
                CustomerPhone = "0900000001",
                ShippingAddress = "1 Nguyen Hue, Quan 1",
                Status = DraftOrderStatus.PendingReview,
                RiskLevel = RiskLevel.Low,
                MerchandiseTotal = 320000m,
                ShippingFee = 0m,
                GrandTotal = 320000m,
                AssignedManagerEmail = PrimaryManagerEmail,
                RequiresManualReview = false
            },
            new DraftOrder
            {
                Id = primaryAltDraftId,
                TenantId = PrimaryTenantId,
                DraftCode = "DR-PRIMARY-ALT-001",
                CustomerIdentityId = primaryAltCustomerId,
                FacebookPSID = "psid-primary-alt",
                FacebookPageId = PrimaryAltPageId,
                CustomerName = "Khach Primary Alt",
                CustomerPhone = "0900000003",
                ShippingAddress = "3 Hai Ba Trung, Quan 1",
                Status = DraftOrderStatus.PendingReview,
                RiskLevel = RiskLevel.Medium,
                MerchandiseTotal = 410000m,
                ShippingFee = 0m,
                GrandTotal = 410000m,
                AssignedManagerEmail = PrimaryManagerEmail,
                RequiresManualReview = true
            },
            new DraftOrder
            {
                Id = secondaryDraftId,
                TenantId = SecondaryTenantId,
                DraftCode = "DR-SECONDARY-001",
                CustomerIdentityId = secondaryCustomerId,
                FacebookPSID = "psid-secondary",
                FacebookPageId = SecondaryPageId,
                CustomerName = "Khach Secondary",
                CustomerPhone = "0900000002",
                ShippingAddress = "2 Le Loi, Quan 3",
                Status = DraftOrderStatus.PendingReview,
                RiskLevel = RiskLevel.Low,
                MerchandiseTotal = 999000m,
                ShippingFee = 0m,
                GrandTotal = 999000m,
                AssignedManagerEmail = SecondaryManagerEmail,
                RequiresManualReview = false
            });

        dbContext.DraftOrderItems.AddRange(
            new DraftOrderItem
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000001"),
                DraftOrderId = primaryDraftId,
                ProductCode = "KCN",
                ProductName = "Kem Chong Nang",
                Quantity = 1,
                UnitPrice = 320000m,
                GiftCode = "GIFT_KCN",
                GiftName = "Mat na duong sang"
            },
            new DraftOrderItem
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000003"),
                DraftOrderId = primaryAltDraftId,
                ProductCode = "KL",
                ProductName = "Kem Lua",
                Quantity = 1,
                UnitPrice = 410000m,
                GiftCode = "GIFT_KL",
                GiftName = "Tinh chat mini"
            },
            new DraftOrderItem
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000002"),
                DraftOrderId = secondaryDraftId,
                ProductCode = "OTHER_1",
                ProductName = "Other Tenant Product",
                Quantity = 1,
                UnitPrice = 999000m
            });

        var primaryCaseId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var secondaryCaseId = Guid.Parse("60000000-0000-0000-0000-000000000002");
        var primaryAltCaseId = Guid.Parse("60000000-0000-0000-0000-000000000003");

        dbContext.HumanSupportCases.AddRange(
            new HumanSupportCase
            {
                Id = primaryCaseId,
                TenantId = PrimaryTenantId,
                FacebookPSID = "psid-case-primary",
                FacebookPageId = PrimaryPageId,
                Reason = SupportCaseReason.PolicyException,
                Summary = "Khach xin them qua ngoai chuong trinh",
                TranscriptExcerpt = "Cho chi them qua nua duoc khong em?",
                AssignedToEmail = PrimaryManagerEmail,
                Status = SupportCaseStatus.Open
            },
            new HumanSupportCase
            {
                Id = primaryAltCaseId,
                TenantId = PrimaryTenantId,
                FacebookPSID = "psid-case-primary-alt",
                FacebookPageId = PrimaryAltPageId,
                Reason = SupportCaseReason.ManualReview,
                Summary = "Case cung tenant khac page",
                TranscriptExcerpt = "Khach cung tenant nhung khac page",
                AssignedToEmail = PrimaryManagerEmail,
                Status = SupportCaseStatus.Open
            },
            new HumanSupportCase
            {
                Id = secondaryCaseId,
                TenantId = SecondaryTenantId,
                FacebookPSID = "psid-case-secondary",
                FacebookPageId = SecondaryPageId,
                Reason = SupportCaseReason.ManualReview,
                Summary = "Case cua tenant khac",
                TranscriptExcerpt = "Khach tenant khac",
                AssignedToEmail = SecondaryManagerEmail,
                Status = SupportCaseStatus.Open
            });

        dbContext.BotConversationLocks.Add(new BotConversationLock
        {
            Id = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            TenantId = PrimaryTenantId,
            FacebookPSID = "psid-case-primary",
            FacebookPageId = PrimaryPageId,
            Reason = "Awaiting human support",
            HumanSupportCaseId = primaryCaseId,
            IsLocked = true
        });
    }
}

public sealed class DevelopmentAdminWebApplicationFactory : CustomWebApplicationFactory
{
    protected override string HostEnvironment => "Development";

    protected override IDictionary<string, string?> AdditionalConfiguration => new Dictionary<string, string?>
    {
        ["Admin:AllowTenantWideVisibilityInDevelopment"] = "true"
    };
}

public sealed class TestMessengerService : IMessengerService
{
    private readonly ConcurrentQueue<SentMessageRecord> _messages = new();

    public IReadOnlyCollection<SentMessageRecord> Messages => _messages.ToArray();

    public Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text, CancellationToken cancellationToken = default)
    {
        var record = new SentMessageRecord(recipientId, text, DateTimeOffset.UtcNow);
        _messages.Enqueue(record);
        return Task.FromResult(new SendMessageResponse(recipientId, $"mid.{Guid.NewGuid():N}"));
    }

    public Task<bool> ReplyToCommentAsync(string commentId, string message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> IsVideoLiveAsync(string videoId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> HideCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<SendMessageResponse> SendQuickReplyAsync(
        string recipientId,
        string text,
        List<QuickReplyButton> quickReplies,
        CancellationToken cancellationToken = default)
    {
        var record = new SentMessageRecord(recipientId, text, DateTimeOffset.UtcNow);
        _messages.Enqueue(record);
        return Task.FromResult(new SendMessageResponse(recipientId, $"mid.{Guid.NewGuid():N}"));
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}

public sealed record SentMessageRecord(string RecipientId, string Text, DateTimeOffset SentAt);

public sealed class TestGeminiService : IGeminiService
{
    private readonly ConcurrentQueue<(string UserId, string Message)> _requests = new();

    public IReadOnlyCollection<(string UserId, string Message)> Requests => _requests.ToArray();

    public Task<string> SendMessageAsync(string userId, string message, List<MessengerWebhook.Services.AI.Models.ConversationMessage> history, MessengerWebhook.Services.AI.Models.GeminiModelType? modelOverride = null, CancellationToken cancellationToken = default)
    {
        _requests.Enqueue((userId, message));
        return Task.FromResult("Da em ho tro chi ngay day a.");
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(string userId, string message, List<MessengerWebhook.Services.AI.Models.ConversationMessage> history, MessengerWebhook.Services.AI.Models.GeminiModelType? modelOverride = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _requests.Enqueue((userId, message));
        yield return "Da em ho tro chi ngay day a.";
        await Task.CompletedTask;
    }

    public MessengerWebhook.Services.AI.Models.GeminiModelType SelectModel(string message)
    {
        return MessengerWebhook.Services.AI.Models.GeminiModelType.FlashLite;
    }

    public Task<MessengerWebhook.Services.AI.Models.ConfirmationDetectionResult> DetectConfirmationAsync(
        string message,
        string contextPhone,
        string contextAddress,
        CancellationToken cancellationToken = default)
    {
        // Simple mock: return true for confirmation keywords
        var normalized = message.ToLowerInvariant();
        var isConfirming = normalized.Contains("dung") || normalized.Contains("ok") || normalized.Contains("van dung");

        return Task.FromResult(new MessengerWebhook.Services.AI.Models.ConfirmationDetectionResult
        {
            IsConfirming = isConfirming,
            Confidence = isConfirming ? 0.9 : 0.1,
            Reason = isConfirming ? "Contains confirmation keywords" : "No confirmation keywords",
            DetectionMethod = "test-mock"
        });
    }

    public void Clear()
    {
        while (_requests.TryDequeue(out _))
        {
        }
    }
}

public sealed class TestEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        return Task.FromResult(Enumerable.Repeat(0.1f, 768).ToArray());
    }

    public Task<List<float[]>> GenerateBatchAsync(List<string> texts, CancellationToken ct = default)
    {
        return Task.FromResult(texts.Select(_ => Enumerable.Repeat(0.1f, 768).ToArray()).ToList());
    }
}

public sealed class TestNobitaClient : INobitaClient
{
    private readonly ConcurrentQueue<NobitaOrderRequest> _submittedOrders = new();
    private readonly ConcurrentDictionary<string, NobitaCustomerInsight> _insights = new(StringComparer.OrdinalIgnoreCase);

    public bool FailNextOrderSubmission { get; set; }
    public string FailureMessage { get; set; } = "Simulated Nobita failure";
    public IReadOnlyCollection<NobitaOrderRequest> SubmittedOrders => _submittedOrders.ToArray();

    public Task<IReadOnlyList<NobitaProductSummary>> GetProductsAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var products = new List<NobitaProductSummary>
        {
            new(101, "KCN", "Kem Chong Nang", 320000m, false),
            new(102, "KL", "Kem Lua", 410000m, false),
            new(103, "COMBO_2", "Combo 2 San Pham", 730000m, false),
            new(201, "OTHER_1", "Other Tenant Product", 999000m, false)
        };

        if (string.IsNullOrWhiteSpace(search))
        {
            return Task.FromResult<IReadOnlyList<NobitaProductSummary>>(products);
        }

        return Task.FromResult<IReadOnlyList<NobitaProductSummary>>(products
            .Where(x => x.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList());
    }

    public Task<NobitaCustomerInsight?> TryGetCustomerInsightAsync(string phoneNumber, string? facebookPsid = null, CancellationToken cancellationToken = default)
    {
        _insights.TryGetValue(phoneNumber, out var insight);
        return Task.FromResult(insight);
    }

    public Task<string?> CreateOrderAsync(NobitaOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (FailNextOrderSubmission)
        {
            FailNextOrderSubmission = false;
            throw new InvalidOperationException(FailureMessage);
        }

        _submittedOrders.Enqueue(request);
        return Task.FromResult<string?>($"NB-{_submittedOrders.Count:000}");
    }

    public void SetInsight(string phoneNumber, NobitaCustomerInsight insight)
    {
        _insights[phoneNumber] = insight;
    }

    public void Reset()
    {
        FailNextOrderSubmission = false;
        FailureMessage = "Simulated Nobita failure";

        while (_submittedOrders.TryDequeue(out _))
        {
        }

        _insights.Clear();
    }
}

public sealed class TestEmailNotificationService : IEmailNotificationService
{
    private readonly ConcurrentQueue<HumanSupportCase> _notifications = new();

    public IReadOnlyCollection<HumanSupportCase> Notifications => _notifications.ToArray();

    public Task SendSupportCaseAssignedAsync(HumanSupportCase supportCase, CancellationToken cancellationToken = default)
    {
        _notifications.Enqueue(supportCase);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_notifications.TryDequeue(out _))
        {
        }
    }
}
