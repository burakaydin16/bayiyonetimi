using System.IdentityModel.Tokens.Jwt;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Services;

namespace MultiTenantSaaS.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ITenantService tenantService, MasterDbContext masterContext)
    {
        // 1. Try to get TenantId from JWT
        string? tenantIdStr = null;
        
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                tenantIdStr = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value;
            }
        }

        // 2. If found, load Tenant info from Master DB
        if (!string.IsNullOrEmpty(tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
        {
            var tenant = await masterContext.Tenants.FindAsync(tenantId);
            if (tenant != null)
            {
                tenantService.CurrentTenant = tenant;
            }
        }

        await _next(context);
    }
}
