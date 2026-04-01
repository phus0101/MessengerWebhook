# Phase 2: Service Registration

**Status:** 🔴 Not Started
**Priority:** High
**Estimated Effort:** 1 day

## Overview

Register PineconeClient và IVectorSearchService trong DI container. Configure .env loading cho PINECONE_API_KEY.

## Related Code Files

**To Modify:**
- `Program.cs` - Service registration, .env mapping, validation

## Implementation Steps

### 1. Register PineconeClient as Singleton

**Location:** `Program.cs` (around line 231)

```csharp
// Register Pinecone client as singleton (thread-safe, reusable)
builder.Services.AddSingleton<PineconeClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PineconeOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "Pinecone:ApiKey is required. Set PINECONE_API_KEY in .env or User Secrets.");
    }

    return new PineconeClient(options.ApiKey);
});

// Register vector search service as scoped (tenant context per request)
builder.Services.AddScoped<IVectorSearchService, PineconeVectorService>();
```

### 2. Map PINECONE_API_KEY from .env

**Location:** `Program.cs` (after line 54, in Development environment block)

```csharp
if (builder.Environment.IsDevelopment())
{
    Env.Load();

    // Map Pinecone API key
    var pineconeApiKey = Environment.GetEnvironmentVariable("PINECONE_API_KEY");
    if (!string.IsNullOrWhiteSpace(pineconeApiKey))
    {
        builder.Configuration["Pinecone:ApiKey"] = pineconeApiKey;
    }
}
```

### 3. Add Startup Validation

**Location:** `Program.cs` (after line 327, after Gemini validation)

```csharp
var pineconeOpts = app.Services.GetRequiredService<IOptions<PineconeOptions>>().Value;
if (string.IsNullOrWhiteSpace(pineconeOpts.ApiKey))
    Log.Warning("Pinecone:ApiKey not configured. Vector search will use pgvector only.");
```

## Todo List

- [ ] Register PineconeClient as singleton
- [ ] Register IVectorSearchService as scoped
- [ ] Map PINECONE_API_KEY from .env
- [ ] Add startup validation warning
- [ ] Test application starts without errors

## Success Criteria

- ✅ PineconeClient registered correctly
- ✅ IVectorSearchService injectable
- ✅ PINECONE_API_KEY loaded from .env
- ✅ Startup validation warns if key missing
- ✅ Application starts successfully
