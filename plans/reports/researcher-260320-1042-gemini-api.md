# Gemini Pro 3.1 API Integration Research Report

**Date:** 2026-03-20
**Focus:** Technical implementation for .NET 8 e-commerce chatbot integration
**Environment:** Windows 11, .NET 8, 16GB RAM

---

## 1. API Authentication & Setup

### Authentication Methods
- **Google AI Studio** (ai.google.dev): API key-based, ideal for prototyping and individual developers
- **Vertex AI** (Google Cloud): Requires GCP project with billing enabled, enterprise-grade

### .NET SDK Options

**Official SDK:**
- [Google Gen AI .NET SDK](https://cloud.google.com/blog/topics/developers-practitioners/introducing-google-gen-ai-net-sdk) - Unified library supporting both Gemini Developer API and Vertex AI
- Supports .NET 8 with async/await patterns
- Available via NuGet package manager

**Community SDK:**
- [Google_GenerativeAI by gunpal5](https://github.com/gunpal5/Google_GenerativeAI) - Most complete C# implementation
- Features: function calling, JSON mode, multi-modal streaming, chat sessions
- Install: `dotnet add package GenerativeAI`

### Setup Steps
1. Obtain API key from [Google AI Studio](https://ai.google.dev)
2. Install NuGet package: `GenerativeAI` or official Google SDK
3. Configure API key in appsettings.json (use User Secrets for development)
4. Initialize client with IHttpClientFactory for connection pooling

### Critical Migration Note
Vertex AI SDK deprecation after June 2026 - new Gemini features only in Gen AI SDK. Recommend starting with Gen AI SDK directly.

---

## 2. Conversation Patterns for E-commerce

### State Management Architecture

**Stateless Approach (Recommended for .NET 8):**
- Client manages conversation history
- Send full context with each request
- Structure: `contents` array with alternating `user`/`model` roles
- Chronological message ordering required
- Best for: Multi-user scenarios, horizontal scaling, stateless APIs

**Implementation Pattern:**
```csharp
// Maintain conversation history in-memory or cache
List<Message> conversationHistory = new();
conversationHistory.Add(new Message { Role = "user", Content = userInput });

// Send entire history with each request
var response = await geminiClient.SendMessageAsync(conversationHistory);
conversationHistory.Add(new Message { Role = "model", Content = response });
```

**Stateful Approach (Interactions API):**
- Server-side history management via `previous_interaction_id`
- Reduces payload size for long conversations
- Available in newer Gemini APIs
- Best for: Long-running sessions, reduced bandwidth

### Multi-Turn Context Retention
- Use `startChat()` to initialize session
- `sendMessage()` automatically appends to history
- Context window: Up to 65,000 output tokens per response
- Maintain conversation state in Redis/MemoryCache for distributed systems

### E-commerce Specific Patterns
- **Product Discovery Flow**: User preferences → Filter questions → Recommendations
- **Size/Fit Consultation**: Measurements → Style preferences → Product matching
- **Order Support**: Order lookup → Issue identification → Resolution steps
- Maintain shopping context (cart, viewed items, preferences) across turns

---

## 3. Prompt Engineering Best Practices

### PTCF Framework (Persona · Task · Context · Format)
Recommended structure for e-commerce chatbot prompts:

**Persona:**
```
You are a knowledgeable fashion consultant for [Store Name], specializing in
helping customers find clothing that matches their style, body type, and occasion needs.
```

**Task:**
```
Help the customer find the perfect outfit by asking clarifying questions about:
- Occasion (casual, formal, business, special event)
- Style preferences (classic, trendy, minimalist, bold)
- Size and fit requirements
- Budget constraints
```

**Context:**
```
Available inventory: [Product categories]
Current promotions: [Active deals]
Customer history: [Previous purchases if available]
```

**Format:**
```
Respond conversationally with:
1. Empathetic acknowledgment
2. 1-2 clarifying questions (max)
3. Specific product recommendations when ready
4. Clear next steps
```

### System Instructions
- Use `systemInstruction` parameter for persistent persona/behavior
- Define constraints: tone, response length, prohibited topics
- Specify output format requirements (JSON for structured data)
- Set boundaries: "Only recommend products from our catalog"

### Key Principles (from [Gemini Mastery Guide](https://blockchain.news/ainews/gemini-mastery-guide-reveals-30-prompt-engineering-principles-and-advanced-ai-workflows))
- Provide clear persona with name, role, characteristics
- Include relevant context in each turn
- Use examples for complex formatting requirements
- Iterate and refine based on actual responses
- Treat prompts as first-class components requiring testing

### E-commerce Optimization
- Pre-load product catalog context in system instructions
- Use structured output for product recommendations (JSON)
- Implement conversation templates for common flows
- A/B test prompt variations for conversion optimization

---

## 4. Streaming vs Batch Responses

### Streaming (Recommended for Chat)
**Use Cases:**
- Real-time chat interfaces
- Interactive product consultation
- Immediate user feedback required

**Performance:**
- Text latency: ~400ms first token
- Progressive rendering improves perceived speed
- Better user experience for conversational flows

**Implementation:**
```csharp
await foreach (var chunk in geminiClient.StreamGenerateContentAsync(prompt))
{
    await hubContext.Clients.User(userId).SendAsync("ReceiveMessage", chunk);
}
```

**Pros:**
- Lower perceived latency
- Can cancel long-running requests
- Better for real-time interactions

**Cons:**
- More complex error handling
- Requires WebSocket/SSE infrastructure
- Higher connection overhead

### Batch Processing
**Use Cases:**
- Bulk product description generation
- Analytics and reporting
- Non-time-sensitive operations

**Performance:**
- 50% cost discount vs real-time API
- Higher rate limits for throughput
- Asynchronous processing (results available later)

**Best For:**
- Processing large datasets
- Background jobs
- Cost optimization for high-volume operations

### Recommendation for E-commerce Chat
Use **streaming** for customer-facing chat interface. Reserve batch API for:
- Nightly product catalog enrichment
- Bulk customer inquiry analysis
- Training data generation

---

## 5. Rate Limits & Cost Optimization

### Pricing (2026)
- **Gemini 3.1 Pro**: ~$2.00 per million input tokens (estimated)
- **Gemini 3.1 Flash-Lite**: $0.25 per million input tokens (8x cheaper)
- **Subscription Tiers**:
  - AI Pro: $19.99/month
  - AI Ultra: $249.99/month

### Rate Limit Dimensions
1. **Requests Per Minute (RPM)**: API call frequency
2. **Requests Per Day (RPD)**: Daily quota (free tier: ~250/day)
3. **Tokens Per Minute (TPM)**: Token throughput limit
4. **Images Per Minute (IPM)**: For multimodal requests

### Tier Structure
- **Free Tier**: 250 requests/day, limited TPM
- **Tier 1-3**: Progressive increases in all dimensions
- Upgrade path: Free → Tier 1 → Tier 2 → Tier 3

### Cost Optimization Strategies

**1. Spend Caps (New in March 2026)**
- Set monthly spending limits in AI Studio
- Prevents unexpected charges
- Configure alerts at 50%, 75%, 90% thresholds

**2. Model Selection**
- Use Flash-Lite for simple queries (size, availability checks)
- Reserve Pro for complex consultation requiring reasoning
- Route requests based on complexity

**3. Context Management**
- Summarize old conversation turns to reduce token count
- Keep only last N turns + summary for long conversations
- Remove redundant context before each request

**4. Caching Strategy**
- Cache common product queries and responses
- Use context caching for frequently accessed product catalogs
- Implement Redis for distributed caching

**5. Batch API for Background Tasks**
- 50% cost discount for non-real-time operations
- Use for analytics, reporting, bulk processing

**6. Token Optimization**
- Use concise system instructions
- Avoid repeating product details in every turn
- Implement reference-based context (product IDs vs full descriptions)

### Cost Estimation for E-commerce
Assumptions: 1000 conversations/day, avg 10 turns, 500 tokens/turn
- Daily tokens: 1000 × 10 × 500 = 5M tokens
- Monthly cost (Pro): 5M × 30 × $2.00/1M = $300/month
- Monthly cost (Flash-Lite): 5M × 30 × $0.25/1M = $37.50/month

**Recommendation:** Hybrid approach - Flash-Lite for 70% of queries, Pro for complex consultations.

---

## 6. Error Handling & Fallback Strategies

### Common Error Types

**429 Rate Limit Exceeded**
- Cause: Exceeded RPM, RPD, TPM, or IPM limits
- Solution: Exponential backoff with jitter
- Fallback: Queue requests, upgrade tier, or use alternative model

**503 Service Overloaded**
- Cause: Google server capacity issues
- Solution: Wait 5-30 minutes with exponential backoff
- Fallback: Switch to alternative model or cached responses

**504 Gateway Timeout**
- Cause: Request too complex or long-running
- Solution: Increase client timeout, reduce prompt complexity
- Fallback: Split request into smaller chunks

**400 Bad Request**
- Cause: Invalid request format, missing parameters
- Solution: Validate request structure before sending
- Fallback: Use default/simplified request format

### Retry Strategy Implementation

**Exponential Backoff Pattern:**
```csharp
public async Task<T> RetryWithBackoffAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    int baseDelayMs = 1000)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            if (i == maxRetries - 1) throw;

            var delay = baseDelayMs * Math.Pow(2, i);
            var jitter = Random.Shared.Next(0, (int)(delay * 0.1));
            await Task.Delay((int)delay + jitter);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            if (i == maxRetries - 1) throw;
            await Task.Delay(baseDelayMs * (i + 1) * 5); // Longer delays for 503
        }
    }
    throw new InvalidOperationException("Max retries exceeded");
}
```

### Fallback Hierarchy

**Level 1: Retry with Backoff**
- Automatic retry for transient errors
- Exponential backoff with jitter
- Max 3 attempts

**Level 2: Model Switching**
- Switch from Pro to Flash-Lite
- Use cached similar responses
- Degrade to simpler prompt

**Level 3: Graceful Degradation**
- Return pre-defined responses for common queries
- Show "AI assistant temporarily unavailable" message
- Queue request for later processing
- Offer human agent escalation

**Level 4: Circuit Breaker**
- Stop sending requests after consecutive failures
- Prevent cascading failures
- Auto-recover after cooldown period

### Monitoring & Alerting
- Track error rates by type (429, 503, 504)
- Monitor average response times
- Alert on: error rate >5%, latency >2s, cost >threshold
- Log failed requests for analysis

### Best Practices (from [Google Cloud Blog](https://cloud.google.com/blog/products/ai-machine-learning/learn-how-to-handle-429-resource-exhaustion-errors-in-your-llms))
- Use retry libraries (Polly for .NET)
- Implement client-side rate limiting
- Add jitter to prevent thundering herd
- Set reasonable timeout values (30-60s for chat)
- Log retry attempts for debugging

---

## 7. .NET 8 Integration Approach

### Architecture Pattern

**Service Layer Structure:**
```
Services/
├── IGeminiService.cs              // Interface
├── GeminiService.cs               // Implementation
├── Models/
│   ├── GeminiRequest.cs
│   ├── GeminiResponse.cs
│   └── ConversationContext.cs
└── Handlers/
    ├── GeminiAuthHandler.cs       // DelegatingHandler for auth
    └── GeminiRetryHandler.cs      // DelegatingHandler for retries
```

### HttpClient Configuration

**Startup/Program.cs:**
```csharp
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddHttpMessageHandler<GeminiAuthHandler>()
.AddHttpMessageHandler<GeminiRetryHandler>()
.SetHandlerLifetime(TimeSpan.FromMinutes(5));

builder.Services.AddMemoryCache(); // For conversation history
builder.Services.AddSingleton<IGeminiService, GeminiService>();
```

### Best Practices (from [Microsoft ASP.NET Core docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-9.0))
- Use IHttpClientFactory for connection pooling
- Keep entire call chain async (no .Result or .Wait())
- Use ConfigureAwait(false) in library code
- Implement cancellation tokens for long operations
- Validate input before API calls

### Configuration Management
```json
// appsettings.json
{
  "Gemini": {
    "ApiKey": "", // Use User Secrets in dev
    "Model": "gemini-3.1-pro",
    "MaxTokens": 2048,
    "Temperature": 0.7,
    "RateLimits": {
      "RequestsPerMinute": 60,
      "TokensPerMinute": 100000
    }
  }
}
```

### Conversation State Management
- Use IMemoryCache for single-instance deployments
- Use Redis/SQL for distributed systems
- Implement sliding expiration (30 min idle timeout)
- Store: conversation history, user context, product preferences

---

## 8. Implementation Recommendations

### Phase 1: Foundation (Week 1)
1. Install Google Gen AI .NET SDK via NuGet
2. Configure API key in User Secrets
3. Implement basic GeminiService with HttpClient
4. Create request/response models
5. Test basic text generation

### Phase 2: Conversation Management (Week 2)
1. Implement conversation history storage (MemoryCache)
2. Build multi-turn chat flow
3. Add system instructions for e-commerce persona
4. Create prompt templates for common scenarios
5. Test context retention across turns

### Phase 3: Optimization (Week 3)
1. Implement streaming responses
2. Add retry logic with exponential backoff
3. Configure rate limiting
4. Set up monitoring and logging
5. Implement cost tracking

### Phase 4: Production Hardening (Week 4)
1. Add circuit breaker pattern
2. Implement fallback strategies
3. Configure spend caps
4. Load testing and performance tuning
5. Security audit (API key protection, input validation)

### Quick Start Code Sample
```csharp
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string _apiKey;

    public async Task<string> SendMessageAsync(string userId, string message)
    {
        // Retrieve conversation history
        var history = _cache.Get<List<Message>>($"conv_{userId}") ?? new();

        // Add user message
        history.Add(new Message { Role = "user", Content = message });

        // Build request
        var request = new
        {
            contents = history.Select(m => new { role = m.Role, parts = new[] { new { text = m.Content } } }),
            systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } },
            generationConfig = new { temperature = 0.7, maxOutputTokens = 2048 }
        };

        // Send to Gemini
        var response = await _httpClient.PostAsJsonAsync(
            $"models/gemini-3.1-pro:generateContent?key={_apiKey}",
            request
        );

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var assistantMessage = result.Candidates[0].Content.Parts[0].Text;

        // Update history
        history.Add(new Message { Role = "model", Content = assistantMessage });
        _cache.Set($"conv_{userId}", history, TimeSpan.FromMinutes(30));

        return assistantMessage;
    }

    private string GetSystemPrompt() =>
        "You are a helpful fashion consultant for our clothing store...";
}
```

---

## 9. Security Considerations

### API Key Protection
- Never commit API keys to source control
- Use User Secrets for development
- Use Azure Key Vault or AWS Secrets Manager for production
- Rotate keys quarterly
- Implement key-per-environment strategy

### Input Validation
- Sanitize user input before sending to API
- Implement max message length (e.g., 2000 chars)
- Filter malicious prompts (prompt injection attempts)
- Rate limit per user to prevent abuse

### Output Filtering
- Validate model responses before displaying
- Filter inappropriate content
- Implement content moderation layer
- Log suspicious outputs

---

## 10. Key Takeaways

### Recommended Stack
- **SDK**: Google Gen AI .NET SDK (official) or GenerativeAI (community)
- **Model**: Gemini 3.1 Flash-Lite for simple queries, Pro for complex consultation
- **State**: Stateless with client-side history management
- **Delivery**: Streaming for real-time chat
- **Caching**: Redis for distributed deployments

### Cost Optimization
- Hybrid model approach (Flash-Lite + Pro) can reduce costs by 60-70%
- Implement spend caps and monitoring
- Use batch API for background tasks
- Cache common responses

### Critical Success Factors
1. Robust error handling with exponential backoff
2. Proper conversation state management
3. Well-crafted system instructions and prompts
4. Monitoring and alerting infrastructure
5. Security-first approach (API key protection, input validation)

### Performance Targets
- First response: <1s (streaming)
- Subsequent turns: <500ms
- Availability: 99.5% (with fallbacks)
- Cost: <$0.10 per conversation (10 turns avg)

---

## Sources

- [Google Gen AI .NET SDK](https://cloud.google.com/blog/topics/developers-practitioners/introducing-google-gen-ai-net-sdk)
- [Google_GenerativeAI Community SDK](https://github.com/gunpal5/Google_GenerativeAI)
- [Firebase Multi-turn Chat Guide](https://firebase.google.com/docs/ai-logic/chat)
- [Google Cloud Multi-turn Conversation](https://cloud.google.com/gemini/docs/conversational-analytics-api/multi-turn-conversation)
- [Gemini Mastery Guide](https://blockchain.news/ainews/gemini-mastery-guide-reveals-30-prompt-engineering-principles-and-advanced-ai-workflows)
- [Gemini API Cost Optimization](https://yingtu.ai/en/blog/gemini-api-batch-vs-caching)
- [Google Cloud 429 Error Handling](https://cloud.google.com/blog/products/ai-machine-learning/learn-how-to-handle-429-resource-exhaustion-errors-in-your-llms)
- [Complete Error Troubleshooting Guide](https://blog.laozhang.ai/en/posts/gemini-api-error-troubleshooting)
- [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-9.0)
- [Vertex AI System Instructions](https://cloud.google.com/vertex-ai/generative-ai/docs/learn/prompts/system-instructions)

---

## Unresolved Questions

1. **Specific rate limits for paid tiers** - Documentation shows free tier limits but paid tier quotas not clearly specified
2. **Context caching pricing** - Cost structure for context caching feature unclear
3. **Interactions API .NET support** - Availability of Interactions API in .NET SDK not confirmed
4. **Function calling for product lookup** - Need to research Gemini function calling for database queries
5. **Multimodal support for product images** - Image input capabilities for "find similar products" feature
