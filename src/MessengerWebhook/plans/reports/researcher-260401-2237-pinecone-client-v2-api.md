# Pinecone.Client v2.0.0 C# SDK API Research Report

**Date:** 2026-04-01
**Package:** Pinecone.Client v2.0.0
**Target:** .NET 8.0 ASP.NET Core
**Use Case:** Vector search for product embeddings (768-dimensional) with multi-tenant isolation

---

## Executive Summary

Pinecone.Client v2.0.0 uses a simplified API with direct method calls on the index client. Key findings:

- **Metadata Type:** `Metadata` class (dictionary-like, indexer-based)
- **No MetadataMap:** Use `Metadata` directly
- **No MetadataValue.From():** Direct assignment works
- **Namespace Support:** Available in `UpsertRequest`, `QueryRequest`, `DeleteRequest`
- **Method Signatures:** `UpsertAsync()`, `QueryAsync()`, `DeleteAsync()` with request objects

---

## 1. Correct Metadata Handling

### Metadata Class Structure

```csharp
// Metadata is a dictionary-like class with indexer support
var metadata = new Metadata
{
    ["productName"] = "Wireless Headphones",
    ["category"] = "Electronics",
    ["price"] = 99.99,
    ["inStock"] = true,
    ["tags"] = new[] { "audio", "wireless", "bluetooth" }
};
```

### Converting from Dictionary<string, object>

```csharp
// Method 1: Direct initialization
public Metadata ConvertToMetadata(Dictionary<string, object> dict)
{
    var metadata = new Metadata();
    foreach (var kvp in dict)
    {
        metadata[kvp.Key] = kvp.Value;
    }
    return metadata;
}

// Method 2: Collection initializer
var metadata = new Metadata
{
    ["key1"] = dict["key1"],
    ["key2"] = dict["key2"]
};
```

### Supported Value Types

```csharp
var metadata = new Metadata
{
    ["stringValue"] = "text",           // string
    ["intValue"] = 42,                  // int
    ["doubleValue"] = 3.14,             // double
    ["boolValue"] = true,               // bool
    ["arrayValue"] = new[] { "a", "b" } // array
};
```

---

## 2. UpsertRequest API

### Basic Upsert

```csharp
using Pinecone;

// Create vectors with metadata
var vectors = new List<Vector>
{
    new Vector
    {
        Id = "product-001",
        Values = new float[768], // Your 768-dimensional embedding
        Metadata = new Metadata
        {
            ["productId"] = "001",
            ["name"] = "Wireless Headphones",
            ["category"] = "Electronics",
            ["price"] = 99.99,
            ["tenantId"] = "tenant-123"
        }
    },
    new Vector
    {
        Id = "product-002",
        Values = new float[768],
        Metadata = new Metadata
        {
            ["productId"] = "002",
            ["name"] = "Smart Watch",
            ["category"] = "Wearables",
            ["price"] = 199.99,
            ["tenantId"] = "tenant-123"
        }
    }
};

// Upsert with namespace for multi-tenant isolation
var upsertRequest = new UpsertRequest
{
    Vectors = vectors,
    Namespace = "tenant-123" // Multi-tenant isolation
};

var response = await index.UpsertAsync(upsertRequest);
Console.WriteLine($"Upserted {response.UpsertedCount} vectors");
```

### Upsert with Sparse-Dense Vectors

```csharp
var vector = new Vector
{
    Id = "product-001",
    Values = new float[768], // Dense embedding
    SparseValues = new SparseValues
    {
        Indices = new uint[] { 1, 5, 10 },
        Values = new[] { 0.5f, 0.8f, 0.3f }
    },
    Metadata = new Metadata
    {
        ["productId"] = "001",
        ["category"] = "Electronics"
    }
};

var upsertRequest = new UpsertRequest
{
    Vectors = new List<Vector> { vector },
    Namespace = "tenant-123"
};

await index.UpsertAsync(upsertRequest);
```

---

## 3. QueryRequest API

### Basic Query

```csharp
// Query by vector
var queryRequest = new QueryRequest
{
    Namespace = "tenant-123",
    Vector = embeddingVector, // float[] with 768 dimensions
    TopK = 10,
    IncludeValues = true,
    IncludeMetadata = true
};

var queryResponse = await index.QueryAsync(queryRequest);

foreach (var match in queryResponse.Matches)
{
    Console.WriteLine($"ID: {match.Id}, Score: {match.Score}");
    if (match.Metadata != null)
    {
        Console.WriteLine($"Product: {match.Metadata["name"]}");
        Console.WriteLine($"Price: {match.Metadata["price"]}");
    }
}
```

### Query with Metadata Filter

```csharp
// Filter by category and price range
var queryRequest = new QueryRequest
{
    Namespace = "tenant-123",
    Vector = embeddingVector,
    TopK = 10,
    IncludeMetadata = true,
    Filter = new Metadata
    {
        ["category"] = new Metadata
        {
            ["$in"] = new[] { "Electronics", "Wearables" }
        },
        ["price"] = new Metadata
        {
            ["$lte"] = 150.0
        }
    }
};

var response = await index.QueryAsync(queryRequest);
```

### Query by ID

```csharp
// Query similar vectors by ID
var queryRequest = new QueryRequest
{
    Namespace = "tenant-123",
    Id = "product-001", // Query by existing vector ID
    TopK = 10,
    IncludeMetadata = true
};

var response = await index.QueryAsync(queryRequest);
```

### Metadata Filter Operators

```csharp
// Supported operators
var filter = new Metadata
{
    // Equality
    ["category"] = "Electronics",

    // In array
    ["status"] = new Metadata
    {
        ["$in"] = new[] { "active", "featured" }
    },

    // Not in array
    ["status"] = new Metadata
    {
        ["$nin"] = new[] { "deleted", "archived" }
    },

    // Comparison operators
    ["price"] = new Metadata
    {
        ["$gte"] = 50.0,  // Greater than or equal
        ["$lte"] = 200.0  // Less than or equal
    },

    // Not equal
    ["category"] = new Metadata
    {
        ["$ne"] = "Discontinued"
    }
};
```

---

## 4. DeleteRequest API

### Delete by IDs

```csharp
// Delete specific vectors
var deleteRequest = new DeleteRequest
{
    Ids = new[] { "product-001", "product-002", "product-003" },
    Namespace = "tenant-123"
};

await index.DeleteAsync(deleteRequest);
```

### Delete by Metadata Filter

```csharp
// Delete all products in a category
var deleteRequest = new DeleteRequest
{
    Filter = new Metadata
    {
        ["category"] = "Discontinued"
    },
    Namespace = "tenant-123"
};

await index.DeleteAsync(deleteRequest);
```

### Delete All in Namespace

```csharp
// Delete all vectors in a namespace
await index.DeleteAsync(new DeleteRequest
{
    DeleteAll = true,
    Namespace = "tenant-123"
});
```

---

## 5. Namespace Usage for Multi-Tenant Isolation

### Pattern for Multi-Tenant Applications

```csharp
public class PineconeVectorService
{
    private readonly Index<Metadata> _index;

    public PineconeVectorService(PineconeClient client, string indexName)
    {
        _index = client.GetIndex(indexName);
    }

    // Upsert with tenant isolation
    public async Task UpsertProductEmbeddings(
        string tenantId,
        List<ProductEmbedding> products)
    {
        var vectors = products.Select(p => new Vector
        {
            Id = $"{tenantId}-{p.ProductId}",
            Values = p.Embedding,
            Metadata = new Metadata
            {
                ["tenantId"] = tenantId,
                ["productId"] = p.ProductId,
                ["name"] = p.Name,
                ["category"] = p.Category,
                ["price"] = p.Price
            }
        }).ToList();

        await _index.UpsertAsync(new UpsertRequest
        {
            Vectors = vectors,
            Namespace = tenantId // Isolate by tenant
        });
    }

    // Query with tenant isolation
    public async Task<List<ProductMatch>> SearchProducts(
        string tenantId,
        float[] queryEmbedding,
        int topK = 10)
    {
        var response = await _index.QueryAsync(new QueryRequest
        {
            Namespace = tenantId, // Only search within tenant
            Vector = queryEmbedding,
            TopK = topK,
            IncludeMetadata = true
        });

        return response.Matches.Select(m => new ProductMatch
        {
            ProductId = m.Metadata["productId"].ToString(),
            Name = m.Metadata["name"].ToString(),
            Score = m.Score
        }).ToList();
    }

    // Delete tenant data
    public async Task DeleteTenantData(string tenantId)
    {
        await _index.DeleteAsync(new DeleteRequest
        {
            DeleteAll = true,
            Namespace = tenantId
        });
    }
}
```

### Namespace Best Practices

1. **Use tenant ID as namespace** for complete isolation
2. **Namespace is optional** - omit for single-tenant scenarios
3. **Namespaces are lightweight** - no performance penalty
4. **Cannot query across namespaces** - design accordingly
5. **Each namespace has independent stats** - use `DescribeIndexStats()` per namespace

---

## 6. Complete Working Example

```csharp
using Pinecone;

public class ProductVectorSearchService
{
    private readonly PineconeClient _pinecone;
    private readonly Index<Metadata> _index;

    public ProductVectorSearchService(string apiKey, string indexName)
    {
        _pinecone = new PineconeClient(apiKey);
        _index = _pinecone.GetIndex(indexName);
    }

    // Upsert product embeddings
    public async Task<int> UpsertProducts(
        string tenantId,
        List<ProductEmbedding> products)
    {
        var vectors = products.Select(p => new Vector
        {
            Id = $"{tenantId}-{p.ProductId}",
            Values = p.Embedding, // 768-dimensional float[]
            Metadata = ConvertToMetadata(p.Metadata)
        }).ToList();

        var response = await _index.UpsertAsync(new UpsertRequest
        {
            Vectors = vectors,
            Namespace = tenantId
        });

        return response.UpsertedCount;
    }

    // Search products
    public async Task<List<ScoredProduct>> SearchProducts(
        string tenantId,
        float[] queryEmbedding,
        Dictionary<string, object> filters = null,
        int topK = 10)
    {
        var request = new QueryRequest
        {
            Namespace = tenantId,
            Vector = queryEmbedding,
            TopK = topK,
            IncludeMetadata = true,
            IncludeValues = false
        };

        if (filters != null)
        {
            request.Filter = ConvertToMetadata(filters);
        }

        var response = await _index.QueryAsync(request);

        return response.Matches.Select(m => new ScoredProduct
        {
            ProductId = m.Metadata["productId"].ToString(),
            Name = m.Metadata["name"].ToString(),
            Category = m.Metadata["category"].ToString(),
            Price = Convert.ToDouble(m.Metadata["price"]),
            Score = m.Score
        }).ToList();
    }

    // Delete products
    public async Task DeleteProducts(string tenantId, List<string> productIds)
    {
        var vectorIds = productIds.Select(id => $"{tenantId}-{id}").ToArray();

        await _index.DeleteAsync(new DeleteRequest
        {
            Ids = vectorIds,
            Namespace = tenantId
        });
    }

    // Helper: Convert Dictionary to Metadata
    private Metadata ConvertToMetadata(Dictionary<string, object> dict)
    {
        var metadata = new Metadata();
        foreach (var kvp in dict)
        {
            metadata[kvp.Key] = kvp.Value;
        }
        return metadata;
    }
}

// DTOs
public class ProductEmbedding
{
    public string ProductId { get; set; }
    public float[] Embedding { get; set; } // 768 dimensions
    public Dictionary<string, object> Metadata { get; set; }
}

public class ScoredProduct
{
    public string ProductId { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public double Price { get; set; }
    public float Score { get; set; }
}
```

---

## 7. Error Handling

### Parallel Upsert with Retry

```csharp
public async Task UpsertWithRetry(
    List<Vector> vectors,
    string namespace,
    int maxRetries = 3)
{
    var retries = maxRetries;

    while (true)
    {
        try
        {
            await _index.UpsertAsync(new UpsertRequest
            {
                Vectors = vectors,
                Namespace = namespace
            });
            break; // Success
        }
        catch (ParallelUpsertException ex) when (retries-- > 0)
        {
            // Filter out failed vectors and retry
            var failedIds = ex.FailedBatchVectorIds.ToHashSet();
            vectors = vectors.Where(v => failedIds.Contains(v.Id)).ToList();

            Console.WriteLine($"Retrying {vectors.Count} failed vectors");
        }
    }
}
```

---

## 8. Key Differences from Previous Issues

### What Changed

| Previous (Incorrect) | v2.0.0 (Correct) |
|---------------------|------------------|
| `MetadataMap` | `Metadata` |
| `MetadataValue.From()` | Direct assignment |
| `new Dictionary<string, MetadataValue>` | `new Metadata { ["key"] = value }` |
| Complex type conversions | Automatic type handling |

### Migration Example

```csharp
// OLD (Incorrect)
var metadata = new MetadataMap
{
    ["key"] = MetadataValue.From("value") // Does not exist
};

// NEW (Correct)
var metadata = new Metadata
{
    ["key"] = "value" // Direct assignment
};
```

---

## 9. Index Client Initialization

```csharp
using Pinecone;

// Initialize client
var pinecone = new PineconeClient("your-api-key");

// List indexes
var indexes = await pinecone.ListIndexes();

// Create serverless index (if needed)
if (!indexes.Contains("product-embeddings"))
{
    await pinecone.CreateServerlessIndex(
        name: "product-embeddings",
        dimension: 768,
        metric: Metric.Cosine,
        cloud: "aws",
        region: "us-east-1"
    );
}

// Get index client (thread-safe, cache as singleton)
var index = await pinecone.GetIndex("product-embeddings");
```

---

## 10. References

- [Official Pinecone .NET SDK Documentation](https://docs.pinecone.io/reference/sdks/dotnet/overview)
- [GitHub Repository: pinecone-io/pinecone-dotnet-client](https://github.com/pinecone-io/pinecone-dotnet-client)
- [NuGet Package: Pinecone.Client](https://www.nuget.org/packages/Pinecone.Client/)
- [Microsoft DevBlogs: Introducing the Pinecone .NET SDK](https://devblogs.microsoft.com/dotnet/introducing-pinecone-dotnet-sdk/)
- [Pinecone API Reference](https://docs.pinecone.io/reference/api/2025-04/data-plane/upsert)

---

## Summary

**Correct API Usage for Pinecone.Client v2.0.0:**

1. Use `Metadata` class (not `MetadataMap`)
2. Direct value assignment (no `MetadataValue.From()`)
3. Namespace parameter in request objects for multi-tenant isolation
4. Method signatures: `UpsertAsync(UpsertRequest)`, `QueryAsync(QueryRequest)`, `DeleteAsync(DeleteRequest)`
5. Metadata supports string, int, double, bool, and array types
6. Index client is thread-safe - inject as singleton

**Multi-Tenant Pattern:**
- Use tenant ID as namespace
- Prefix vector IDs with tenant ID
- Store tenant ID in metadata for double verification
- Delete tenant data with `DeleteAll = true` in namespace
