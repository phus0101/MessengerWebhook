# Phase 05: Program.cs Modularization — Breaking the Configuration Monolith

**Date**: 2026-05-12 16:45
**Severity**: Medium
**Component**: Application startup / Dependency injection / Service configuration
**Status**: Resolved

## What Happened

Program.cs shrank from 763 lines to 56 lines (93% reduction). Extracted 9 domain-focused DI extension methods under `src/MessengerWebhook/Configuration/ServiceRegistration/`, each handling one logical concern:

- **PersistenceRegistration** — DbContext, repositories, SessionManager, ITenantContext
- **ObservabilityRegistration** — Serilog/Seq logging, Telegram alerts, health checks (WebApplicationBuilder extension)
- **CacheServicesRegistration** — MemoryCache, response caching, Redis decorator (runs AFTER AiServices)
- **AiServicesRegistration** — Gemini/VertexAI options, HttpClient config, Pinecone, HybridSearchService, RAG
- **BackgroundServicesRegistration** — Hosted services, Channel<MessagingEvent>, keyed SemaphoreSlim
- **MessengerServicesRegistration** — Facebook options, MessengerService HttpClient, Nobita service
- **SalesPipelineRegistration** — 22 state handlers, domain services, product pipeline, R-02–R-05 extracted services
- **AdminModuleRegistration** — Cookie auth, admin services, INobitaSubmissionService
- **ApplicationInitializationExtensions** — DB migrations (dev-only), Gemini/Pinecone validation, admin bootstrap

Moved endpoint mapping to `src/MessengerWebhook/Endpoints/WebhookEndpointExtensions.cs`. Fixed keyed SemaphoreSlim to prevent collision between concurrent request limits and background job semaphores.

## The Brutal Truth

Program.cs had become a 763-line junkyard. Every time we needed to register a service, it went there. Every time a new concern emerged—monitoring, background jobs, admin—it got bolted on at the end. The real frustration: **finding DI registration order dependencies by trial-and-error**, not by reading coherent code.

The painful discovery: **CacheServicesRegistration must run AFTER AiServicesRegistration** because the Redis decorator wraps the concrete HybridSearchService type. We only figured this out when startup failed with "type not registered." The order wasn't documented; it was implicit in the code. That's a maintenance landmine.

## Technical Details

**Non-obvious architectural choices:**

1. **Double-registration pattern in CacheServicesRegistration:**
   ```csharp
   services.AddSingleton<HybridSearchService>(...);  // Concrete
   services.AddSingleton<IHybridSearchService>(...);  // Interface (Redis decorator)
   ```
   Both are required. The decorator wraps the concrete type; consumers depend on the interface. This is intentional, not a mistake.

2. **ObservabilityRegistration extends WebApplicationBuilder, not IServiceCollection:**
   ```csharp
   public static WebApplicationBuilder AddObservabilityServices(this WebApplicationBuilder builder)
   ```
   Serilog setup requires `builder.Host.UseSerilog()`. Can't be done with IServiceCollection alone. This exception breaks the pattern but is unavoidable.

3. **INobitaSubmissionService in AdminModuleRegistration:**
   The service lives in `Services/Admin` namespace, so it belongs with other admin registrations, not MessengerServicesRegistration. Logical grouping > namespace depth.

4. **Keyed SemaphoreSlim fix:**
   ```csharp
   services.AddKeyedSingleton<SemaphoreSlim>("ConcurrentRequestLimit", new SemaphoreSlim(100));
   services.AddKeyedSingleton<SemaphoreSlim>("BackgroundJobSlots", new SemaphoreSlim(10));
   ```
   .NET 8 keyed DI prevents name collisions. Before: both used the same key, creating a race condition.

## What We Tried

- ✓ Initial split: tried 12 extension files; regrouped based on dependency order → 9 files
- ✓ Registration order verification: ran startup with Seq logging to trace DI container builds
- ✓ Backward compatibility: kept `??` fallback patterns in handlers; 849 unit tests still pass without modification

## Root Cause Analysis

Why did Program.cs become a monolith?

1. **No DI convention enforced** — New features just registered at the bottom, no structure
2. **Implicit ordering dependencies** — Developers didn't know (or document) that CacheServices depends on AiServices
3. **Mixed concerns** — Infrastructure (Serilog), domain (sales pipeline), admin features all at the same nesting level

This is a structural problem, not a feature problem. It compounds: the bigger Program.cs gets, the more expensive it is to find or add a registration.

## Lessons Learned

- **Ordering matters in DI; document it explicitly** — Add comments like "// MUST run after AiServicesRegistration" at the top of each extension
- **Keyed DI in .NET 8 is non-negotiable for multi-concern containers** — Prevents silent collisions
- **WebApplicationBuilder extensions are a valid exception to IServiceCollection-only rule** — Acknowledge and document where infrastructure (not just domain) services live
- **Service registration is code smell detector** — If a registration file grows past ~100 lines, the concern is too broad; re-split

## Next Steps

1. **Add inline documentation** — Each registration extension should have a comment block listing dependencies and required ordering
2. **Document the CacheServices ordering rule** — Add explicit comment in both CacheServicesRegistration and AiServicesRegistration
3. **Consider: registration validation at startup** — Could add a helper to verify all types are registered before app starts (prevents runtime "type not found" errors)
4. **Monitor future growth** — If SalesPipelineRegistration exceeds 200 lines, extract state handlers into separate file

---

**Metrics:**
- Program.cs: 763 → 56 lines (−707, −93%)
- DI files created: 9
- Tests passing: 849/849 (0 failures)
- Compile errors: 0
- Commit: 7a378a1

*The lesson wasn't elegance. It was making implicit dependencies explicit so the next person doesn't debug a startup failure at 2am wondering why a decorator can't find its wrapped type.*
