using MessengerWebhook.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MessengerWebhook.IntegrationTests.Fixtures;

/// <summary>
/// Database fixture using Testcontainers for integration tests
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Use ankane/pgvector image which includes pgvector extension
        // Use unique database name to avoid conflicts between test runs
        var dbName = $"test_db_{Guid.NewGuid():N}";

        _container = new PostgreSqlBuilder("ankane/pgvector:latest")
            .WithDatabase(dbName)
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Create DbContext
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new MessengerBotDbContext(options);

        Console.WriteLine("[DatabaseFixture] Enabling pgvector extension...");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");
        Console.WriteLine("[DatabaseFixture] pgvector extension enabled");

        Console.WriteLine("[DatabaseFixture] Applying migrations...");
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Console.WriteLine($"[DatabaseFixture] Pending migrations: {string.Join(", ", pendingMigrations)}");

        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Products"
            ADD COLUMN IF NOT EXISTS "Embedding" vector(768);
            """);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS idx_products_embedding
            ON "Products" USING ivfflat ("Embedding" vector_cosine_ops)
            WITH (lists = 100);
            """);
        Console.WriteLine("[DatabaseFixture] Migrations applied");

        // Dispose context to clear EF Core model cache
        await context.DisposeAsync();

        Console.WriteLine("[DatabaseFixture] Database setup complete");
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    public MessengerBotDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new MessengerBotDbContext(options);
    }
}
