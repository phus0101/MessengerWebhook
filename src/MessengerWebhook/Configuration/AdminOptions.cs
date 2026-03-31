namespace MessengerWebhook.Configuration;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string CookieName { get; set; } = "messenger_admin";
    public string LoginPath { get; set; } = "/admin/login";
    public string BootstrapEmail { get; set; } = string.Empty;
    public string BootstrapPassword { get; set; } = string.Empty;
    public string BootstrapFullName { get; set; } = "Admin";
    public bool SeedDemoWorkspaceIfMissing { get; set; }
    public bool AllowTenantWideVisibilityInDevelopment { get; set; }
    public string BootstrapTenantCode { get; set; } = "mui-xu-dev";
    public string BootstrapTenantName { get; set; } = "Mui Xu Local Dev";
    public string BootstrapPageId { get; set; } = "DEV_PAGE_1";
    public string BootstrapPageName { get; set; } = "Mui Xu Dev Page";
}
