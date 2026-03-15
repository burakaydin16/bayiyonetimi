using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;
using MultiTenantSaaS.Services;
using System.Security.Claims;

namespace MultiTenantSaaS.Controllers;

[ApiController]
[Route("api/super-admin")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly MasterDbContext _masterContext;
    private readonly AppDbContext _appContext;
    private readonly IConfiguration _configuration;
    private readonly ITenantService _tenantService;
    private readonly ILogger<SuperAdminController> _logger;

    public SuperAdminController(MasterDbContext masterContext, AppDbContext appContext, IConfiguration configuration, ITenantService tenantService, ILogger<SuperAdminController> logger)
    {
        _masterContext = masterContext;
        _appContext = appContext;
        _configuration = configuration;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _masterContext.Tenants.ToListAsync();
        return Ok(tenants.Select(t => new { t.Id, t.Name, t.Email, t.ReferenceCode, t.IsApproved, t.SchemaName }));
    }

    [HttpPost("approve-tenant/{tenantId}")]
    public async Task<IActionResult> ApproveTenant(Guid tenantId)
    {
        var tenant = await _masterContext.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound("Tenant not found");
        if (tenant.IsApproved) return BadRequest("Tenant is already approved");

        try
        {
            _logger.LogInformation("Approving tenant {TenantId}. Creating Schema and Tables for {Schema}...", tenantId, tenant.SchemaName);

            // 1. Create Schema manually (idempotent)
            var connString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new Npgsql.NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{tenant.SchemaName}\"";
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // 2. Run Migrations for the new schema
            // CurrentTenant property triggers the TenantSchemaInterceptor to set search_path
            _tenantService.CurrentTenant = tenant;
            
            _logger.LogInformation("Running migrations for schema {Schema}...", tenant.SchemaName);
            await _appContext.Database.MigrateAsync();

            _logger.LogInformation("Schema and Tables initialized for {Schema}", tenant.SchemaName);

            // 3. Create Admin User (idempotent check)
            var adminEmail = tenant.Email;
            var adminUser = await _appContext.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
            
            if (adminUser == null)
            {
                _logger.LogInformation("Creating Admin User for {Schema}...", tenant.SchemaName);
                adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = adminEmail,
                    PasswordHash = tenant.PasswordHash, // Use hashed password from registration
                    Role = "Admin",
                    Permissions = "*"
                };
                _appContext.Users.Add(adminUser);
                await _appContext.SaveChangesAsync();
            }

            // 4. Mark as approved in Master DB
            tenant.IsApproved = true;
            await _masterContext.SaveChangesAsync();

            _logger.LogInformation("ApproveTenant Success for {TenantId}", tenantId);
            return Ok(new { Message = "Firma başarıyla onaylandı ve sistem hazırlandı!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveTenant failed for {TenantId}", tenantId);
            return BadRequest(new { Error = ex.Message, Step = "Initialization" });
        }
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] MultiTenantSaaS.Controllers.ChangePasswordDto dto)
    {
        var adminIdString = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(adminIdString, out Guid adminId)) return Unauthorized();

        var admin = await _masterContext.SuperAdmins.FindAsync(adminId);
        if (admin == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, admin.PasswordHash))
            return BadRequest(new { Message = "Incorrect old password." });

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _masterContext.SaveChangesAsync();

        return Ok(new { Message = "Password changed successfully." });
    }

    [HttpPost("login-as-tenant/{tenantId}")]
    public async Task<IActionResult> LoginAsTenant(Guid tenantId)
    {
        var tenant = await _masterContext.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound("Tenant not found");
        if (!tenant.IsApproved) return BadRequest("Cannot login as unapproved tenant");

        // Generate a token that looks like a regular tenant user token
        // but with a special Role if desired, or just "Admin" to have full access.
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "super_secret_key_1234567890123456");
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()), // System user
                new Claim(ClaimTypes.Email, $"superadmin-as-{tenant.ReferenceCode}"),
                new Claim("tenantId", tenant.Id.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new { Token = tokenString });
    }
}
