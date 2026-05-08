using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MessengerWebhook.UnitTests.Data.Repositories;

public class ProductRepositoryTests : IDisposable
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ProductRepository _repository;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public ProductRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TestMessengerBotDbContext(options);
        _repository = new ProductRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetActiveByCodeAsync_GlobalProduct_ReturnsActiveProduct()
    {
        await SeedProductsAsync();

        var result = await _repository.GetActiveByCodeAsync("MN_GLOBAL", _tenantId);

        Assert.NotNull(result);
        Assert.Equal("mask-global", result.Id);
    }

    [Fact]
    public async Task GetActiveByIdAsync_GlobalProduct_ReturnsActiveProduct()
    {
        await SeedProductsAsync();

        var result = await _repository.GetActiveByIdAsync("mask-global", _tenantId);

        Assert.NotNull(result);
        Assert.Equal("MN_GLOBAL", result.Code);
    }

    [Fact]
    public async Task GetActiveRelatedAsync_ReturnsActiveTenantAndGlobalProductsOnly()
    {
        await SeedProductsAsync();

        var results = await _repository.GetActiveRelatedAsync(
            _tenantId,
            ProductCategory.Cosmetics,
            new[] { "mat na", "duong am" },
            3);

        Assert.Contains(results, product => product.Id == "mask-active");
        Assert.Contains(results, product => product.Id == "mask-global");
        Assert.DoesNotContain(results, product => product.Id == "mask-inactive");
        Assert.DoesNotContain(results, product => product.Id == "mask-other-tenant");
    }

    [Fact]
    public async Task GetActiveRelatedAsync_GlobalProduct_ReturnsAlongsideTenantSpecificProduct()
    {
        await SeedProductsAsync();

        var results = await _repository.GetActiveRelatedAsync(
            _tenantId,
            ProductCategory.Cosmetics,
            new[] { "mat na", "duong am" },
            3);

        Assert.Contains(results, product => product.Id == "mask-global");
        Assert.DoesNotContain(results, product => product.Id == "mask-other-tenant");
    }

    [Fact]
    public async Task GetActiveRelatedAsync_GlobalProductionMask_ReturnsForAccentInsensitiveNeed()
    {
        _dbContext.Products.Add(new Product
        {
            Id = "mask-production-global",
            Code = "MN",
            Name = "Mặt Nạ Ngủ Dưỡng Ẩm",
            Description = "Mặt nạ ngủ cấp ẩm chuyên sâu, phục hồi da qua đêm",
            Category = ProductCategory.Cosmetics,
            BasePrice = 180000m,
            IsActive = true,
            TenantId = null
        });
        await _dbContext.SaveChangesAsync();

        var results = await _repository.GetActiveRelatedAsync(
            _tenantId,
            ProductCategory.Cosmetics,
            new[] { "mat na", "dưỡng ẩm" },
            3);

        Assert.Contains(results, product => product.Id == "mask-production-global");
    }

    [Fact]
    public async Task GetActiveRelatedAsync_TenantProduct_RanksBeforeGlobalProduct()
    {
        await SeedProductsAsync();

        var results = await _repository.GetActiveRelatedAsync(
            _tenantId,
            ProductCategory.Cosmetics,
            new[] { "mat na", "duong am" },
            3);

        Assert.True(results.FindIndex(product => product.Id == "mask-active") < results.FindIndex(product => product.Id == "mask-global"));
    }

    [Fact]
    public async Task GetActiveRelatedAsync_EmptyCriteria_ReturnsEmptyList()
    {
        await SeedProductsAsync();

        var results = await _repository.GetActiveRelatedAsync(_tenantId, null, Array.Empty<string>(), 3);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetActiveRelatedAsync_MaxCount_IsBounded()
    {
        _dbContext.Products.AddRange(Enumerable.Range(1, 6).Select(index => new Product
        {
            Id = $"mask-{index}",
            Code = $"MN{index}",
            Name = $"Mat na cap am {index}",
            Description = "Mat na duong am",
            Category = ProductCategory.Cosmetics,
            BasePrice = 100000m + index,
            IsActive = true,
            TenantId = _tenantId
        }));
        await _dbContext.SaveChangesAsync();

        var results = await _repository.GetActiveRelatedAsync(_tenantId, ProductCategory.Cosmetics, new[] { "mat na" }, 99);

        Assert.Equal(5, results.Count);
    }

    private async Task SeedProductsAsync()
    {
        _dbContext.Products.AddRange(
            new Product
            {
                Id = "mask-active",
                Code = "MN_ACTIVE",
                Name = "Mặt nạ dưỡng ẩm Rau Má",
                Description = "Mặt nạ cấp ẩm cho da khô",
                Category = ProductCategory.Cosmetics,
                BasePrice = 120000m,
                IsActive = true,
                TenantId = _tenantId
            },
            new Product
            {
                Id = "mask-inactive",
                Code = "MN_INACTIVE",
                Name = "Mặt nạ dưỡng ẩm ngưng bán",
                Description = "Mặt nạ cấp ẩm",
                Category = ProductCategory.Cosmetics,
                BasePrice = 90000m,
                IsActive = false,
                TenantId = _tenantId
            },
            new Product
            {
                Id = "mask-other-tenant",
                Code = "MN_OTHER",
                Name = "Mặt nạ dưỡng ẩm tenant khác",
                Description = "Mặt nạ cấp ẩm",
                Category = ProductCategory.Cosmetics,
                BasePrice = 130000m,
                IsActive = true,
                TenantId = _otherTenantId
            },
            new Product
            {
                Id = "mask-global",
                Code = "MN_GLOBAL",
                Name = "Mặt nạ ngủ dưỡng ẩm global",
                Description = "Mặt nạ cấp ẩm dùng chung catalog",
                Category = ProductCategory.Cosmetics,
                BasePrice = 125000m,
                IsActive = true,
                TenantId = null
            },
            new Product
            {
                Id = "serum-active",
                Code = "SRM_ACTIVE",
                Name = "Serum dưỡng ẩm",
                Description = "Serum cấp ẩm",
                Category = ProductCategory.Cosmetics,
                BasePrice = 220000m,
                IsActive = true,
                TenantId = _tenantId
            });

        await _dbContext.SaveChangesAsync();
    }

    private class TestMessengerBotDbContext : MessengerBotDbContext
    {
        public TestMessengerBotDbContext(DbContextOptions<MessengerBotDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<ProductEmbedding>();
        }
    }
}
