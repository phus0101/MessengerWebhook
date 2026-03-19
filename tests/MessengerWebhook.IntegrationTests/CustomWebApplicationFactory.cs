using MessengerWebhook.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MessengerWebhook.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Facebook:AppSecret"] = "test_app_secret",
                ["Facebook:PageAccessToken"] = "test_page_access_token",
                ["Webhook:VerifyToken"] = "test_verify_token_12345"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Mock IMessengerService to avoid real API calls
            var mockMessengerService = new Mock<IMessengerService>();
            mockMessengerService
                .Setup(m => m.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new SendMessageResponse("test_recipient", "test_message_id"));

            services.AddSingleton(mockMessengerService.Object);

            // Remove GraphApiHealthCheck to avoid real API calls in tests
            services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(options =>
            {
                var graphApiCheck = options.Registrations.FirstOrDefault(r => r.Name == "graph_api");
                if (graphApiCheck != null)
                {
                    options.Registrations.Remove(graphApiCheck);
                }
            });
        });
    }
}
