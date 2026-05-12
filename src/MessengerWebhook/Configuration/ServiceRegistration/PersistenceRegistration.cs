using MessengerWebhook.Data;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class PersistenceRegistration
{
    internal static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MessengerBotDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseVector()));

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<FacebookPageConfigLookupService>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISkinProfileRepository, SkinProfileRepository>();
        services.AddScoped<IConversationMessageRepository, ConversationMessageRepository>();
        services.AddScoped<IIngredientCompatibilityRepository, IngredientCompatibilityRepository>();
        services.AddScoped<IVectorSearchRepository, VectorSearchRepository>();
        services.AddScoped<IGiftRepository, GiftRepository>();
        services.AddScoped<IProductGiftMappingRepository, ProductGiftMappingRepository>();

        services.AddScoped<ISessionManager, SessionManager>();

        return services;
    }
}
