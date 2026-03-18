using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
    }
}
