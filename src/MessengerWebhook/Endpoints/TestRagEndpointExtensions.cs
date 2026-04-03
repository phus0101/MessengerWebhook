using MessengerWebhook.Services.RAG;
using Microsoft.AspNetCore.Mvc;

namespace MessengerWebhook.Endpoints;

public static class TestRagEndpointExtensions
{
    public static IEndpointRouteBuilder MapTestRagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/test-rag", async (
            [FromBody] TestRagRequest request,
            [FromServices] IRAGService ragService) =>
        {
            var result = await ragService.RetrieveContextAsync(
                request.Query,
                topK: request.TopK ?? 5);

            return Results.Ok(new
            {
                query = request.Query,
                topK = request.TopK ?? 5,
                productIds = result.ProductIds,
                formattedContext = result.FormattedContext,
                metrics = new
                {
                    totalLatency = result.Metrics.TotalLatency.TotalMilliseconds,
                    retrievalLatency = result.Metrics.RetrievalLatency.TotalMilliseconds,
                    productsRetrieved = result.Metrics.ProductsRetrieved,
                    cacheHit = result.Metrics.CacheHit,
                    source = result.Metrics.Source
                }
            });
        })
        .WithName("TestRAG")
        .WithTags("Admin");

        return app;
    }

    public record TestRagRequest(string Query, int? TopK);
}
