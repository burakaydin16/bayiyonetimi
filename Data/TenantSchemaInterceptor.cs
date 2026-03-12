using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MultiTenantSaaS.Services;

namespace MultiTenantSaaS.Data;

public class TenantSchemaInterceptor : DbConnectionInterceptor
{
    private readonly ITenantService _tenantService;

    public TenantSchemaInterceptor(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_tenantService.CurrentTenant != null)
        {
            using var cmd = connection.CreateCommand();
            // SET search_path ensures all subsequent queries use this schema by default
            cmd.CommandText = $"SET search_path TO \"{_tenantService.CurrentTenant.SchemaName}\"";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
