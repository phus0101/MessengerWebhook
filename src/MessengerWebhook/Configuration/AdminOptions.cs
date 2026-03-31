namespace MessengerWebhook.Configuration;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string CookieName { get; set; } = "messenger_admin";
    public string LoginPath { get; set; } = "/admin/login";
    public string BootstrapEmail { get; set; } = string.Empty;
    public string BootstrapPassword { get; set; } = string.Empty;
    public string BootstrapFullName { get; set; } = "Admin";
}
