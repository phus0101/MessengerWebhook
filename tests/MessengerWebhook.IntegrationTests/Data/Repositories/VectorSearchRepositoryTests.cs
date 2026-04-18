using FluentAssertions;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;

namespace MessengerWebhook.IntegrationTests.Data.Repositories;

/// <summary>
/// Integration tests for VectorSearchRepository with real PostgreSQL + pgvector
/// </summary>
public class VectorSearchRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public VectorSearchRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        ResetVectorTables();
    }

    private VectorSearchRepository CreateRepository()
    {
        var context = _fixture.CreateDbContext();
        return new VectorSearchRepository(context, NullLogger<VectorSearchRepository>.Instance);
    }

    private void ResetVectorTables()
    {
        using var context = _fixture.CreateDbContext();
        context.Database.ExecuteSqlRaw("DELETE FROM \"ProductEmbeddings\"");
        context.Database.ExecuteSqlRaw("DELETE FROM \"Products\"");
    }

    private async Task<Product> SeedProductWithEmbedding(float[] embedding, string name = "Test Product", bool isActive = true)
    {
        await using var context = _fixture.CreateDbContext();
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Code = $"VECTOR_{Guid.NewGuid():N}",
            Name = name,
            Description = "Test description",
            Brand = "Test Brand",
            Category = ProductCategory.Cosmetics,
            BasePrice = 100,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var sql = @"
            INSERT INTO ""ProductEmbeddings"" (""Id"", ""TenantId"", ""ProductId"", ""Embedding"", ""CreatedAt"", ""UpdatedAt"")
            VALUES (@id, @tenantId, @productId, @embedding::vector, @createdAt, @updatedAt);";

        var now = DateTime.UtcNow;
        var embeddingParam = new NpgsqlParameter("@embedding", NpgsqlDbType.Array | NpgsqlDbType.Real)
        {
            Value = embedding
        };

        await context.Database.ExecuteSqlRawAsync(
            sql,
            new object[]
            {
                new NpgsqlParameter("@id", Guid.NewGuid()),
                new NpgsqlParameter("@tenantId", DBNull.Value),
                new NpgsqlParameter("@productId", product.Id),
                embeddingParam,
                new NpgsqlParameter("@createdAt", now),
                new NpgsqlParameter("@updatedAt", now)
            });

        return product;
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_ValidEmbedding_ReturnsResults()
    {
        var embedding1 = Enumerable.Repeat(0.1f, 768).ToArray();
        var embedding2 = Enumerable.Repeat(0.9f, 768).ToArray();

        await SeedProductWithEmbedding(embedding1, "Product 1");
        await SeedProductWithEmbedding(embedding2, "Product 2");

        var repository = CreateRepository();
        var queryEmbedding = Enumerable.Repeat(0.1f, 768).ToArray();

        var results = await repository.SearchSimilarProductsAsync(queryEmbedding, limit: 5, similarityThreshold: 0.5);

        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Name == "Product 1");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_BelowThreshold_FiltersCorrectly()
    {
        var embedding1 = new float[768];
        for (int i = 0; i < 384; i++) embedding1[i] = 1.0f;
        for (int i = 384; i < 768; i++) embedding1[i] = 0.0f;

        await SeedProductWithEmbedding(embedding1, "Product 1");

        var repository = CreateRepository();
        var queryEmbedding = new float[768];
        for (int i = 0; i < 384; i++) queryEmbedding[i] = 0.0f;
        for (int i = 384; i < 768; i++) queryEmbedding[i] = 1.0f;

        var results = await repository.SearchSimilarProductsAsync(queryEmbedding, limit: 5, similarityThreshold: 0.5);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_InvalidDimensions_ThrowsArgumentException()
    {
        var repository = CreateRepository();
        var invalidEmbedding = new float[100];

        var act = async () => await repository.SearchSimilarProductsAsync(invalidEmbedding);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*768 dimensions*");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_NullEmbedding_ThrowsArgumentNullException()
    {
        var repository = CreateRepository();

        var act = async () => await repository.SearchSimilarProductsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("queryEmbedding");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_OnlyReturnsActiveProducts()
    {
        var embedding = Enumerable.Repeat(0.5f, 768).ToArray();

        await SeedProductWithEmbedding(embedding, "Active Product", isActive: true);
        await SeedProductWithEmbedding(embedding, "Inactive Product", isActive: false);

        var repository = CreateRepository();

        var results = await repository.SearchSimilarProductsAsync(embedding, limit: 10, similarityThreshold: 0.5);

        results.Should().NotContain(p => p.Name == "Inactive Product");
        results.Should().Contain(p => p.Name == "Active Product");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_RespectsLimit()
    {
        var embedding = Enumerable.Repeat(0.5f, 768).ToArray();

        for (int j = 0; j < 10; j++)
        {
            await SeedProductWithEmbedding(embedding, $"Product {j}");
        }

        var repository = CreateRepository();

        var results = await repository.SearchSimilarProductsAsync(embedding, limit: 3, similarityThreshold: 0.5);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateProductEmbeddingAsync_ValidData_UpdatesSuccessfully()
    {
        var initialEmbedding = Enumerable.Repeat(0.1f, 768).ToArray();
        var product = await SeedProductWithEmbedding(initialEmbedding, "Test Product");

        var newEmbedding = Enumerable.Repeat(0.9f, 768).ToArray();
        var repository = CreateRepository();

        await repository.UpdateProductEmbeddingAsync(product.Id, newEmbedding);

        await using var context = _fixture.CreateDbContext();
        var sql = @"SELECT ""Embedding""::real[] FROM ""ProductEmbeddings"" WHERE ""ProductId"" = @productId";
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var param = command.CreateParameter();
        param.ParameterName = "@productId";
        param.Value = product.Id;
        command.Parameters.Add(param);

        await context.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var persistedEmbedding = (float[])reader.GetValue(0);
        persistedEmbedding.Should().HaveCount(768);
        persistedEmbedding[0].Should().Be(0.9f);
    }

    [Fact]
    public async Task UpdateProductEmbeddingAsync_ProductNotFound_ThrowsInvalidOperationException()
    {
        var repository = CreateRepository();
        var embedding = new float[768];
        var nonExistentId = Guid.NewGuid().ToString();

        var act = async () => await repository.UpdateProductEmbeddingAsync(nonExistentId, embedding);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateProductEmbeddingAsync_InvalidDimensions_ThrowsArgumentException()
    {
        var embedding = new float[768];
        var product = await SeedProductWithEmbedding(embedding, "Test Product");

        var repository = CreateRepository();
        var invalidEmbedding = new float[100];

        var act = async () => await repository.UpdateProductEmbeddingAsync(product.Id, invalidEmbedding);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*768 dimensions*");
    }
}
