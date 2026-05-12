using MessengerWebhook.Data;
using MessengerWebhook.Services.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class ApplicationInitializationExtensions
{
    /// <summary>
    /// Runs startup tasks: DB migrations, config validation, admin bootstrap.
    /// Call immediately after app.Build() before middleware setup.
    /// </summary>
    internal static async Task InitializeAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            if (dbContext.Database.IsRelational())
            {
                try
                {
                    await dbContext.Database.MigrateAsync();
                    Log.Information("Database migrations applied successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to apply database migrations");
                    throw;
                }
            }
            else
            {
                Log.Information("Skipping database migrations because provider is not relational");
            }
        }

        var geminiOpts = app.Services.GetRequiredService<IOptions<GeminiOptions>>().Value;
        if (string.IsNullOrWhiteSpace(geminiOpts.ApiKey))
            throw new InvalidOperationException("Gemini:ApiKey is required. Configure via User Secrets or environment variables.");

        var pineconeOpts = app.Services.GetRequiredService<IOptions<PineconeOptions>>().Value;
        if (string.IsNullOrWhiteSpace(pineconeOpts.ApiKey))
        {
            Log.Fatal("Pinecone:ApiKey is required. Set PINECONE_API_KEY in .env or User Secrets.");
            throw new InvalidOperationException("Pinecone:ApiKey is required.");
        }
        if (string.IsNullOrWhiteSpace(pineconeOpts.IndexName))
        {
            Log.Fatal("Pinecone:IndexName is required in appsettings.json");
            throw new InvalidOperationException("Pinecone:IndexName is required in appsettings.json");
        }
        Log.Information("Pinecone configuration validated: Index={IndexName}", pineconeOpts.IndexName);

        using var adminScope = app.Services.CreateScope();
        await adminScope.ServiceProvider.GetRequiredService<IAdminAuthService>().EnsureBootstrapManagerAsync();
    }
}
