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
    }

    private VectorSearchRepository CreateRepository()
    {
        var context = _fixture.CreateDbContext();
        return new VectorSearchRepository(context, NullLogger<VectorSearchRepository>.Instance);
    }

    private async Task<Product> SeedProductWithEmbedding(float[] embedding, string name = "Test Product")
    {
        await using var context = _fixture.CreateDbContext();
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = "Test description",
            Brand = "Test Brand",
            Category = ProductCategory.Cosmetics,
            BasePrice = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Update embedding via raw SQL (since Embedding is [NotMapped])
        var sql = @"UPDATE ""Products"" SET ""Embedding"" = @embedding::vector WHERE ""Id"" = @productId";
        var embeddingParam = new NpgsqlParameter("@embedding", NpgsqlDbType.Array | NpgsqlDbType.Real)
        {
            Value = embedding
        };
        await context.Database.ExecuteSqlRawAsync(sql, embeddingParam, new NpgsqlParameter("@productId", product.Id));

        return product;
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_ValidEmbedding_ReturnsResults()
    {
        // Arrange
        var embedding1 = new float[768];
        var embedding2 = new float[768];
        for (int i = 0; i < 768; i++)
        {
            embedding1[i] = 0.1f;
            embedding2[i] = 0.9f; // Very different
        }

        await SeedProductWithEmbedding(embedding1, "Product 1");
        await SeedProductWithEmbedding(embedding2, "Product 2");

        var repository = CreateRepository();
        var queryEmbedding = new float[768];
        for (int i = 0; i < 768; i++) queryEmbedding[i] = 0.1f; // Similar to embedding1

        // Act
        var results = await repository.SearchSimilarProductsAsync(queryEmbedding, limit: 5, similarityThreshold: 0.5);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Name == "Product 1");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_BelowThreshold_FiltersCorrectly()
    {
        // Arrange - Create orthogonal vectors (cosine similarity = 0)
        var embedding1 = new float[768];
        for (int i = 0; i < 384; i++) embedding1[i] = 1.0f;
        for (int i = 384; i < 768; i++) embedding1[i] = 0.0f;

        await SeedProductWithEmbedding(embedding1, "Product 1");

        var repository = CreateRepository();
        var queryEmbedding = new float[768];
        for (int i = 0; i < 384; i++) queryEmbedding[i] = 0.0f;
        for (int i = 384; i < 768; i++) queryEmbedding[i] = 1.0f; // Orthogonal to embedding1

        // Act - High threshold should filter out low similarity results
        var results = await repository.SearchSimilarProductsAsync(queryEmbedding, limit: 5, similarityThreshold: 0.5);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_InvalidDimensions_ThrowsArgumentException()
    {
        // Arrange
        var repository = CreateRepository();
        var invalidEmbedding = new float[100]; // Wrong dimension

        // Act
        var act = async () => await repository.SearchSimilarProductsAsync(invalidEmbedding);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*768 dimensions*");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_NullEmbedding_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var act = async () => await repository.SearchSimilarProductsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("queryEmbedding");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_OnlyReturnsActiveProducts()
    {
        // Arrange
        var embedding = new float[768];
        for (int i = 0; i < 768; i++) embedding[i] = 0.5f;

        var activeProduct = await SeedProductWithEmbedding(embedding, "Active Product");

        // Create inactive product
        await using var context = _fixture.CreateDbContext();
        var inactiveProduct = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Inactive Product",
            Description = "Test",
            Brand = "Test",
            Category = ProductCategory.Cosmetics,
            BasePrice = 100,
            Embedding = embedding,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Products.Add(inactiveProduct);
        await context.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var results = await repository.SearchSimilarProductsAsync(embedding, limit: 10, similarityThreshold: 0.5);

        // Assert
        results.Should().NotContain(p => p.Name == "Inactive Product");
        results.Should().Contain(p => p.Name == "Active Product");
    }

    [Fact]
    public async Task SearchSimilarProductsAsync_RespectsLimit()
    {
        // Arrange
        var embedding = new float[768];
        for (int i = 0; i < 768; i++) embedding[i] = 0.5f;

        for (int j = 0; j < 10; j++)
        {
            await SeedProductWithEmbedding(embedding, $"Product {j}");
        }

        var repository = CreateRepository();

        // Act
        var results = await repository.SearchSimilarProductsAsync(embedding, limit: 3, similarityThreshold: 0.5);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateProductEmbeddingAsync_ValidData_UpdatesSuccessfully()
    {
        // Arrange
        var initialEmbedding = new float[768];
        for (int i = 0; i < 768; i++) initialEmbedding[i] = 0.1f;

        var product = await SeedProductWithEmbedding(initialEmbedding, "Test Product");

        var newEmbedding = new float[768];
        for (int i = 0; i < 768; i++) newEmbedding[i] = 0.9f;

        var repository = CreateRepository();

        // Act
        await repository.UpdateProductEmbeddingAsync(product.Id, newEmbedding);

        // Assert - Verify embedding via raw SQL (since Embedding is [NotMapped])
        await using var context = _fixture.CreateDbContext();
        var sql = @"SELECT ""Embedding""::real[] FROM ""Products"" WHERE ""Id"" = @productId";
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var param = command.CreateParameter();
        param.ParameterName = "@productId";
        param.Value = product.Id;
        command.Parameters.Add(param);

        await context.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var embedding = (float[])reader.GetValue(0);
        embedding.Should().NotBeNull();
        embedding.Should().HaveCount(768);
        embedding[0].Should().Be(0.9f);
    }

    [Fact]
    public async Task UpdateProductEmbeddingAsync_ProductNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var repository = CreateRepository();
        var embedding = new float[768];
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var act = async () => await repository.UpdateProductEmbeddingAsync(nonExistentId, embedding);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateProductEmbeddingAsync_InvalidDimensions_ThrowsArgumentException()
    {
        // Arrange
        var embedding = new float[768];
        var product = await SeedProductWithEmbedding(embedding, "Test Product");

        var repository = CreateRepository();
        var invalidEmbedding = new float[100];

        // Act
        var act = async () => await repository.UpdateProductEmbeddingAsync(product.Id, invalidEmbedding);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*768 dimensions*");
    }
}
