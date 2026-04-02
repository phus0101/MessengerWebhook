using MessengerWebhook.Services.Cache;

namespace MessengerWebhook.UnitTests.Services.Cache;

public class CacheKeyGeneratorTests
{
    private readonly CacheKeyGenerator _generator;

    public CacheKeyGeneratorTests()
    {
        _generator = new CacheKeyGenerator();
    }

    [Fact]
    public void GenerateEmbeddingKey_SameText_ReturnsSameKey()
    {
        // Arrange
        var text = "Kem chống nắng cho da dầu";

        // Act
        var key1 = _generator.GenerateEmbeddingKey(text);
        var key2 = _generator.GenerateEmbeddingKey(text);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateEmbeddingKey_DifferentText_ReturnsDifferentKeys()
    {
        // Arrange
        var text1 = "Kem chống nắng";
        var text2 = "Sữa rửa mặt";

        // Act
        var key1 = _generator.GenerateEmbeddingKey(text1);
        var key2 = _generator.GenerateEmbeddingKey(text2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateEmbeddingKey_StartsWithPrefix()
    {
        // Arrange
        var text = "test query";

        // Act
        var key = _generator.GenerateEmbeddingKey(text);

        // Assert
        Assert.StartsWith("emb:", key);
    }

    [Fact]
    public void GenerateResultKey_SameInputs_ReturnsSameKey()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var tenantId = Guid.NewGuid();
        var filter = new Dictionary<string, object> { { "category", "skincare" } };

        // Act
        var key1 = _generator.GenerateResultKey(embedding, tenantId, filter);
        var key2 = _generator.GenerateResultKey(embedding, tenantId, filter);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateResultKey_DifferentEmbedding_ReturnsDifferentKeys()
    {
        // Arrange
        var embedding1 = new float[] { 0.1f, 0.2f, 0.3f };
        var embedding2 = new float[] { 0.4f, 0.5f, 0.6f };
        var tenantId = Guid.NewGuid();

        // Act
        var key1 = _generator.GenerateResultKey(embedding1, tenantId);
        var key2 = _generator.GenerateResultKey(embedding2, tenantId);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateResultKey_DifferentTenant_ReturnsDifferentKeys()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();

        // Act
        var key1 = _generator.GenerateResultKey(embedding, tenantId1);
        var key2 = _generator.GenerateResultKey(embedding, tenantId2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateResultKey_DifferentFilter_ReturnsDifferentKeys()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var tenantId = Guid.NewGuid();
        var filter1 = new Dictionary<string, object> { { "category", "skincare" } };
        var filter2 = new Dictionary<string, object> { { "category", "makeup" } };

        // Act
        var key1 = _generator.GenerateResultKey(embedding, tenantId, filter1);
        var key2 = _generator.GenerateResultKey(embedding, tenantId, filter2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateResultKey_NullFilter_UsesNoneHash()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var tenantId = Guid.NewGuid();

        // Act
        var key = _generator.GenerateResultKey(embedding, tenantId, null);

        // Assert
        Assert.Contains(":none", key);
    }

    [Fact]
    public void GenerateResultKey_StartsWithPrefix()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var tenantId = Guid.NewGuid();

        // Act
        var key = _generator.GenerateResultKey(embedding, tenantId);

        // Assert
        Assert.StartsWith("results:", key);
    }

    [Fact]
    public void GenerateResponseKey_SameInputs_ReturnsSameKey()
    {
        // Arrange
        var query = "Kem chống nắng nào tốt?";
        var context = "user context";
        var productIds = new List<string> { "prod1", "prod2" };

        // Act
        var key1 = _generator.GenerateResponseKey(query, context, productIds);
        var key2 = _generator.GenerateResponseKey(query, context, productIds);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateResponseKey_DifferentQuery_ReturnsDifferentKeys()
    {
        // Arrange
        var query1 = "Kem chống nắng";
        var query2 = "Sữa rửa mặt";
        var context = "user context";
        var productIds = new List<string> { "prod1" };

        // Act
        var key1 = _generator.GenerateResponseKey(query1, context, productIds);
        var key2 = _generator.GenerateResponseKey(query2, context, productIds);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateResponseKey_DifferentProductIds_ReturnsDifferentKeys()
    {
        // Arrange
        var query = "Kem chống nắng";
        var context = "user context";
        var productIds1 = new List<string> { "prod1", "prod2" };
        var productIds2 = new List<string> { "prod3", "prod4" };

        // Act
        var key1 = _generator.GenerateResponseKey(query, context, productIds1);
        var key2 = _generator.GenerateResponseKey(query, context, productIds2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateResponseKey_StartsWithPrefix()
    {
        // Arrange
        var query = "test query";
        var context = "test context";
        var productIds = new List<string> { "prod1" };

        // Act
        var key = _generator.GenerateResponseKey(query, context, productIds);

        // Assert
        Assert.StartsWith("response:", key);
    }
}
