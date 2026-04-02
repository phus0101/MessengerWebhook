using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MessengerWebhook.Data;

/// <summary>
/// Design-time factory for EF Core migrations
/// </summary>
public class MessengerBotDbContextFactory : IDesignTimeDbContextFactory<MessengerBotDbContext>
{
    public MessengerBotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MessengerBotDbContext>();

        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=messenger_bot;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        return new MessengerBotDbContext(optionsBuilder.Options);
    }
}
