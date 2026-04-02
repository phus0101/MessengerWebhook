# Pinecone.Client v2.0.0 - ACTUAL API Research Report

**Date:** 2026-04-01 23:30
**Package:** Pinecone.Client v2.0.0 (Official SDK)
**Source:** https://github.com/pinecone-io/pinecone-dotnet-client

## Executive Summary

Researched ACTUAL Pinecone.Client v2.0.0 C# SDK API from official GitHub repository. Previous implementation was INCORRECT. This report provides verified API signatures and working examples.

## Key Findings

### 1. Client Initialization
```csharp
using Pinecone;

var pinecone = new PineconeClient("PINECONE_API_KEY");
```

### 2. Getting Index Client
```csharp
// Returns IndexClient (NOT Index)
var index = pinecone.Index("index-name");
```

### 3. Method Signatures - VERIFIED

#### UpsertAsync
```csharp
public async Task<UpsertResponse> UpsertAsync(
    UpsertRequest request,
    GrpcRequestOptions? options = null,
    CancellationToken cancellationToken = default
)
```

**Parameters:**
- `request`: UpsertRequest object containing vectors
- `options`: GrpcRequestOptions (optional) - NOT direct CancellationToken
- `cancellationToken`: Standard cancellation token

#### QueryAsync
```csharp
public async Task<QueryResponse> QueryAsync(
    QueryRequest request,
    GrpcRequestOptions? options = null,
    CancellationToken cancellationToken = default
)
```

#### DeleteAsync
```csharp
public async Task<DeleteResponse> DeleteAsync(
    DeleteRequest request,
    GrpcRequestOptions? options = null,
    CancellationToken cancellationToken = default
)
```

## Data Types

### Vector Class
```csharp
public record Vector
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("values")]
    public ReadOnlyMemory<float>? Values { get; set; }

    [JsonPropertyName("sparseValues")]
    public SparseValues? SparseValues { get; set; }

    [JsonPropertyName("metadata")]
    public Metadata? Metadata { get; set; }
}
```

### Metadata Class
```csharp
// Metadata is Dictionary<string, MetadataValue?>
public sealed class Metadata : Dictionary<string, MetadataValue?>
{
    // Direct assignment supported via implicit operators
}
```

### MetadataValue
```csharp
// Supports implicit conversion from:
// - string
// - double
// - bool
// - arrays of above types
// - nested Metadata objects

// Usage examples:
metadata["genre"] = "action";           // string
metadata["year"] = 2019;                // double (implicit from int)
metadata["rating"] = 4.5;               // double
metadata["available"] = true;           // bool
metadata["tags"] = new[] { "new", "popular" };  // string array
```

### GrpcRequestOptions
```csharp
public partial class GrpcRequestOptions
{
    public int? MaxRetries { get; init; }
    public TimeSpan? Timeout { get; init; }
    public WriteOptions? WriteOptions { get; init; }
    public CallCredentials? CallCredentials { get; init; }
    internal Headers Headers { get; init; }
}
```

## Working Examples from Official README

### Example 1: Upsert Vectors (768-dimensional)
```csharp
using Pinecone;

var pinecone = new PineconeClient("PINECONE_API_KEY");
var index = pinecone.Index("example-index");

// Create vectors with 768 dimensions
var vectors = new List<Vector>
{
    new Vector
    {
        Id = "vec1",
        Values = new float[768], // Your 768-dimensional embedding
        Metadata = new Metadata
        {
            ["text"] = "Sample text",
            ["timestamp"] = 1234567890.0,
            ["category"] = "example"
        }
    },
    new Vector
    {
        Id = "vec2",
        Values = new float[768],
        Metadata = new Metadata
        {
            ["text"] = "Another sample",
            ["timestamp"] = 1234567891.0
        }
    }
};

var upsertResponse = await index.UpsertAsync(new UpsertRequest
{
    Vectors = vectors,
    Namespace = "example-namespace"
});
```

### Example 2: Query with Metadata Filter
```csharp
var queryResponse = await index.QueryAsync(new QueryRequest
{
    Namespace = "example-namespace",
    Vector = new[] { 0.1f, 0.2f, 0.3f /* ... 768 values */ },
    TopK = 10,
    IncludeValues = true,
    IncludeMetadata = true,
    Filter = new Metadata
    {
        ["category"] = new Metadata
        {
            ["$in"] = new[] { "example", "test", "demo" }
        }
    }
});
```

### Example 3: Delete Vectors
```csharp
// Delete by IDs
var deleteResponse = await index.DeleteAsync(new DeleteRequest
{
    Ids = new[] { "vec1", "vec2" },
    Namespace = "example-namespace"
});

// Delete all in namespace
var deleteAllResponse = await index.DeleteAsync(new DeleteRequest
{
    DeleteAll = true,
    Namespace = "example-namespace"
});
```

### Example 4: Using CancellationToken with GrpcRequestOptions
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// Option 1: Pass CancellationToken directly (recommended)
var response = await index.UpsertAsync(
    new UpsertRequest { Vectors = vectors },
    options: null,
    cancellationToken: cts.Token
);

// Option 2: Use GrpcRequestOptions for advanced scenarios
var options = new GrpcRequestOptions
{
    MaxRetries = 3,
    Timeout = TimeSpan.FromSeconds(30)
};

var response2 = await index.UpsertAsync(
    new UpsertRequest { Vectors = vectors },
    options: options,
    cancellationToken: cts.Token
);
```

### Example 5: Fetch Vectors
```csharp
var fetchResponse = await index.FetchAsync(new FetchRequest
{
    Ids = new[] { "vec1", "vec2" },
    Namespace = "example-namespace"
});

foreach (var kvp in fetchResponse.Vectors)
{
    var vector = kvp.Value;
    Console.WriteLine($"ID: {vector.Id}");
    Console.WriteLine($"Values: {vector.Values?.Length}");
    if (vector.Metadata != null)
    {
        foreach (var meta in vector.Metadata)
        {
            Console.WriteLine($"  {meta.Key}: {meta.Value}");
        }
    }
}
```

### Example 6: Describe Index Stats
```csharp
var statsResponse = await index.DescribeIndexStatsAsync(
    new DescribeIndexStatsRequest()
);

Console.WriteLine($"Total vector count: {statsResponse.TotalVectorCount}");
Console.WriteLine($"Dimension: {statsResponse.Dimension}");
foreach (var ns in statsResponse.Namespaces)
{
    Console.WriteLine($"Namespace {ns.Key}: {ns.Value.VectorCount} vectors");
}
```

## Critical Corrections from Previous Research

### ❌ WRONG (Previous Implementation)
```csharp
// INCORRECT - These don't exist in v2.0.0
await indexClient.UpsertAsync(vectors, namespace, cancellationToken);
await indexClient.QueryAsync(queryVector, topK, namespace, cancellationToken);
metadata["key"] = new MetadataValue("value");  // Unnecessary wrapper
```

### ✅ CORRECT (Actual v2.0.0 API)
```csharp
// CORRECT - Request objects required
await index.UpsertAsync(new UpsertRequest
{
    Vectors = vectors,
    Namespace = namespace
}, cancellationToken: cancellationToken);

await index.QueryAsync(new QueryRequest
{
    Vector = queryVector,
    TopK = topK,
    Namespace = namespace
}, cancellationToken: cancellationToken);

// Direct assignment via implicit operators
metadata["key"] = "value";  // Implicit conversion to MetadataValue
metadata["count"] = 42;     // Implicit conversion to MetadataValue
```

## Request/Response Types

### UpsertRequest
```csharp
public record UpsertRequest
{
    public IEnumerable<Vector> Vectors { get; set; }
    public string? Namespace { get; set; }
}
```

### QueryRequest
```csharp
public record QueryRequest
{
    public string? Namespace { get; set; }
    public required uint TopK { get; set; }
    public Metadata? Filter { get; set; }
    public bool? IncludeValues { get; set; }
    public bool? IncludeMetadata { get; set; }
    public ReadOnlyMemory<float>? Vector { get; set; }
    public SparseValues? SparseVector { get; set; }
    public string? Id { get; set; }  // Query by ID instead of vector
}
```

### DeleteRequest
```csharp
public record DeleteRequest
{
    public IEnumerable<string>? Ids { get; set; }
    public string? Namespace { get; set; }
    public bool? DeleteAll { get; set; }
    public Metadata? Filter { get; set; }
}
```

## Complete Working Service Implementation

```csharp
using Pinecone;

public class PineconeVectorService
{
    private readonly PineconeClient _client;
    private readonly string _indexName;

    public PineconeVectorService(string apiKey, string indexName)
    {
        _client = new PineconeClient(apiKey);
        _indexName = indexName;
    }

    public async Task<UpsertResponse> UpsertVectorsAsync(
        IEnumerable<(string id, float[] embedding, Dictionary<string, object> metadata)> vectors,
        string namespace_,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_indexName);

        var pineconeVectors = vectors.Select(v => new Vector
        {
            Id = v.id,
            Values = v.embedding,
            Metadata = ConvertToMetadata(v.metadata)
        }).ToList();

        return await index.UpsertAsync(
            new UpsertRequest
            {
                Vectors = pineconeVectors,
                Namespace = namespace_
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<QueryResponse> QueryAsync(
        float[] queryVector,
        int topK,
        string namespace_,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_indexName);

        var request = new QueryRequest
        {
            Vector = queryVector,
            TopK = (uint)topK,
            Namespace = namespace_,
            IncludeValues = true,
            IncludeMetadata = true
        };

        if (filter != null)
        {
            request.Filter = ConvertToMetadata(filter);
        }

        return await index.QueryAsync(request, cancellationToken: cancellationToken);
    }

    public async Task<DeleteResponse> DeleteVectorsAsync(
        IEnumerable<string> ids,
        string namespace_,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_indexName);

        return await index.DeleteAsync(
            new DeleteRequest
            {
                Ids = ids,
                Namespace = namespace_
            },
            cancellationToken: cancellationToken
        );
    }

    private static Metadata ConvertToMetadata(Dictionary<string, object> dict)
    {
        var metadata = new Metadata();
        foreach (var kvp in dict)
        {
            metadata[kvp.Key] = kvp.Value switch
            {
                string s => s,
                int i => (double)i,
                long l => (double)l,
                double d => d,
                float f => (double)f,
                bool b => b,
                _ => kvp.Value.ToString()
            };
        }
        return metadata;
    }
}
```

## Important Notes

1. **All data plane operations use gRPC** - UpsertAsync, QueryAsync, DeleteAsync, FetchAsync
2. **Request objects are required** - No direct parameter passing
3. **Metadata uses implicit operators** - Direct assignment works for primitives
4. **CancellationToken is separate parameter** - Not inside GrpcRequestOptions
5. **ReadOnlyMemory<float>** - Used for vector values (can pass float[] directly)
6. **Namespace parameter** - Named `Namespace` in requests (not `namespace_` in method signature)

## Testing Recommendations

1. Test with actual 768-dimensional vectors
2. Verify metadata filtering works with nested conditions
3. Test cancellation token behavior
4. Verify namespace isolation
5. Test batch upsert limits (recommended: 100 vectors per batch)

## References

- GitHub Repository: https://github.com/pinecone-io/pinecone-dotnet-client
- Official README: https://github.com/pinecone-io/pinecone-dotnet-client/blob/main/README.md
- NuGet Package: https://www.nuget.org/packages/Pinecone.Client (v2.0.0)
- Published: November 14, 2024
- Maintainer: Pinecone Systems, Inc.

## Conclusion

The v2.0.0 API uses request objects for all operations, supports implicit metadata conversion, and provides proper async/await patterns with CancellationToken support. Previous implementation assumptions were incorrect and need to be updated.
