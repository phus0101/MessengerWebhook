using Microsoft.AspNetCore.Http;

namespace MessengerWebhook.Services.Tenants;

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantId;
    private string? _facebookPageId;
    private string? _managerEmail;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId =>
        _tenantId ??
        ReadGuid(_httpContextAccessor.HttpContext?.Items["TenantId"]);

    public string? FacebookPageId =>
        _facebookPageId ??
        _httpContextAccessor.HttpContext?.Items["FacebookPageId"] as string;

    public string? ManagerEmail =>
        _managerEmail ??
        _httpContextAccessor.HttpContext?.Items["ManagerEmail"] as string;

    public bool IsResolved => TenantId.HasValue;

    public void Initialize(Guid? tenantId, string? facebookPageId, string? managerEmail)
    {
        _tenantId = tenantId;
        _facebookPageId = facebookPageId;
        _managerEmail = managerEmail;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        httpContext.Items["TenantId"] = tenantId;
        httpContext.Items["FacebookPageId"] = facebookPageId;
        httpContext.Items["ManagerEmail"] = managerEmail;
    }

    public void Clear()
    {
        _tenantId = null;
        _facebookPageId = null;
        _managerEmail = null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        httpContext.Items.Remove("TenantId");
        httpContext.Items.Remove("FacebookPageId");
        httpContext.Items.Remove("ManagerEmail");
    }

    private static Guid? ReadGuid(object? value)
    {
        return value switch
        {
            Guid guid => guid,
            null => null,
            _ => null
        };
    }
}
