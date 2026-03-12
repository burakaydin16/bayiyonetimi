using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Services;

public interface ITenantService
{
    Tenant? CurrentTenant { get; set; }
    string? ConnectionString { get; }
}

public class TenantService : ITenantService
{
    public Tenant? CurrentTenant { get; set; }
    public string? ConnectionString { get; set; }
}
