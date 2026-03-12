using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;
using MultiTenantSaaS.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MultiTenantSaaS.Controllers;

public record LoginDto(string TenantName, string Email, string Password);
public record RegisterTenantDto(string Name, string Email, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MasterDbContext _masterContext;
    private readonly AppDbContext _appContext;
    private readonly IConfiguration _configuration;
    private readonly ITenantService _tenantService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(MasterDbContext masterContext, AppDbContext appContext, IConfiguration configuration, ITenantService tenantService, ILogger<AuthController> logger)
    {
        _masterContext = masterContext;
        _appContext = appContext;
        _configuration = configuration;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpPost("register-tenant")]
    public async Task<IActionResult> RegisterTenant(RegisterTenantDto dto)
    {
        _logger.LogInformation("Starting RegisterTenant for {Email}", dto.Email);

        // 1. Create Tenant in Master DB
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = dto.Password,
            SchemaName = $"tenant_{Guid.NewGuid().ToString("N")}"
        };

        try
        {
            _logger.LogInformation("Step 1: Saving Tenant to Master DB...");
            _masterContext.Tenants.Add(tenant);
            await _masterContext.SaveChangesAsync();
            _logger.LogInformation("Step 1: Success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step 1 Failed");
            var realError = ex;
            while (realError.InnerException != null) realError = realError.InnerException;
            return BadRequest(new { Step = "MasterSave", Error = realError.Message });
        }

        // 2. Create Schema and Tables
        var connString = _configuration.GetConnectionString("DefaultConnection");
        _logger.LogInformation("Step 2: Opening manual connection for Schema creation...");

        using (var conn = new Npgsql.NpgsqlConnection(connString))
        {
            await conn.OpenAsync();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    _logger.LogInformation("Step 2.1: Creating Schema {Schema}...", tenant.SchemaName);
                    cmd.CommandText = $"CREATE SCHEMA \"{tenant.SchemaName}\"";
                    await cmd.ExecuteNonQueryAsync();

                    _logger.LogInformation("Step 2.2: Setting search_path...");
                    cmd.CommandText = $"SET search_path TO \"{tenant.SchemaName}\"";
                    await cmd.ExecuteNonQueryAsync();

                    _logger.LogInformation("Step 2.3: Generating and executing Table Scripts...");
                    var sqlScript = _appContext.Database.GenerateCreateScript();
                    cmd.CommandText = sqlScript;
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Step 2: Success.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step 2 Failed");
                return BadRequest(new { Step = "SchemaOrTableCreation", Error = ex.Message });
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        // 3. Create Admin User
        _logger.LogInformation("Step 3: Creating Admin User in new schema...");
        _tenantService.CurrentTenant = tenant;

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = dto.Password,
            Role = "Admin"
        };

        try
        {
            _appContext.Users.Add(adminUser);
            _logger.LogInformation("Step 3.1: Calling SaveChangesAsync for Admin User...");
            await _appContext.SaveChangesAsync();
            _logger.LogInformation("Step 3: Success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step 3 Failed");
            var realError = ex;
            while (realError.InnerException != null) realError = realError.InnerException;
            return BadRequest(new { Step = "AdminUserCreation", Error = realError.Message });
        }

        _logger.LogInformation("RegisterTenant completed successfully for {Email}", dto.Email);
        return Ok(new { tenant.Id, tenant.SchemaName, Message = "Registration successful" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // 1. Find Tenant
        var tenant = await _masterContext.Tenants.FirstOrDefaultAsync(t => t.Name == dto.TenantName);
        if (tenant == null) return Unauthorized("Tenant not found");

        // 2. Switch Context to Tenant Schema (Manual for Login check)
        // We can't use the Interceptor here because we don't have a token yet!
        // So we manually set the search path on the connection.
        var connection = _appContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SET search_path TO \"{tenant.SchemaName}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. Find User in Tenant Schema
        var user = await _appContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || user.PasswordHash != dto.Password) return Unauthorized("Invalid credentials");

        // 4. Generate JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "super_secret_key_1234567890123456"); // Use config!
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("tenantId", tenant.Id.ToString()), // Critical for Middleware
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new { Token = tokenString });
    }
}
