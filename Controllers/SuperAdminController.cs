using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;
using MultiTenantSaaS.Services;
using System.Security.Claims;

namespace MultiTenantSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
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

        // Create Schema and Tables
        var connString = _configuration.GetConnectionString("DefaultConnection");
        _logger.LogInformation("Opening manual connection for Schema creation...");

        using (var conn = new Npgsql.NpgsqlConnection(connString))
        {
            await conn.OpenAsync();
            using (var transaction = await conn.BeginTransactionAsync())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = transaction;

                        _logger.LogInformation("Creating Schema {Schema}...", tenant.SchemaName);
                        cmd.CommandText = $"CREATE SCHEMA \"{tenant.SchemaName}\"";
                        await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation("Setting search_path...");
                        cmd.CommandText = $"SET search_path TO \"{tenant.SchemaName}\"";
                        await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation("Generating and executing Table Scripts...");
                        var sqlScript = _appContext.Database.GenerateCreateScript();
                        cmd.CommandText = sqlScript;
                        await cmd.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                    _logger.LogInformation("Schema creation Success.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Schema creation Failed");
                    return BadRequest(new { Step = "SchemaCreation", Error = ex.Message });
                }
            }
        }

        // Create Admin User in the new schema
        _logger.LogInformation("Creating Admin User in new schema...");
        _tenantService.CurrentTenant = tenant;

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = tenant.Email,
            PasswordHash = tenant.PasswordHash,
            Role = "Admin",
            Permissions = "*"
        };

        try
        {
            // Set the search path manually for the context just in case
            var connection = _appContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"SET search_path TO \"{tenant.SchemaName}\"";
                await cmd.ExecuteNonQueryAsync();
            }

            _appContext.Users.Add(adminUser);
            await _appContext.SaveChangesAsync();
            
            // Mark as approved in Master DB
            tenant.IsApproved = true;
            await _masterContext.SaveChangesAsync();

            _logger.LogInformation("ApproveTenant Success.");
            return Ok(new { Message = "Tenant approved and initialized successfully!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin user creation failed");
            return BadRequest(new { Step = "AdminUserCreation", Error = ex.Message });
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
