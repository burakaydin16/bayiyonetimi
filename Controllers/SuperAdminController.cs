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
        if (tenant == null) return NotFound("Firma bulunamadı.");
        if (tenant.IsApproved) return BadRequest("Bu firma zaten onaylanmış.");

        try
        {
            _logger.LogInformation("Firma onaylanıyor. Şema hazırlanıyor: {Schema}...", tenant.SchemaName);

            var connString = _configuration.GetConnectionString("DefaultConnection");
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

                            // 1. Varsa eski (hatalı/yarım kalmış) şemayı temizle ve yenisini oluştur
                            _logger.LogInformation("Creating clean schema...");
                            cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{tenant.SchemaName}\" CASCADE; CREATE SCHEMA \"{tenant.SchemaName}\";";
                            await cmd.ExecuteNonQueryAsync();

                            // 2. search_path'i bu şemaya ayarla
                            cmd.CommandText = $"SET search_path TO \"{tenant.SchemaName}\"";
                            await cmd.ExecuteNonQueryAsync();

                            // 3. Tabloları oluştur (GenerateCreateScript kullanarak)
                            _logger.LogInformation("Creating tables from script...");
                            var sqlScript = _appContext.Database.GenerateCreateScript();
                            cmd.CommandText = sqlScript;
                            await cmd.ExecuteNonQueryAsync();

                            // 4. Admin kullanıcısı zaten Master DB'de (public.users) kayıtlı olduğu için
                            // buraya tekrar eklemeye gerek yok. Sadece şema ve tabloları hazırlıyoruz.
                            _logger.LogInformation("Database tables created successfully.");
                        }
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Veritabanı hazırlama hatası");
                        return BadRequest(new { Step = "DatabaseInit", Error = ex.Message });
                    }
                }
            }

            // 5. Master DB'de onayla
            tenant.IsApproved = true;
            await _masterContext.SaveChangesAsync();

            _logger.LogInformation("Firma '{Name}' başarıyla hazırlandı.", tenant.Name);
            return Ok(new { Message = "Firma başarıyla onaylandı ve veritabanı hazırlandı!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Genel onaylama hatası");
            return BadRequest(new { Step = "ApproveProcess", Error = ex.Message });
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

        // Find the owner user in public schema
        var owner = await _masterContext.Users.FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Role == "Admin");

        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "super_secret_key_1234567890123456");
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, owner?.Id.ToString() ?? Guid.Empty.ToString()),
                new Claim(ClaimTypes.Email, owner?.Email ?? $"superadmin-as-{tenant.ReferenceCode}"),
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

    [HttpPost("reset-tenant-password/{tenantId}")]
    public async Task<IActionResult> ResetTenantPassword(Guid tenantId, [FromBody] ResetTenantPasswordDto dto)
    {
        var tenant = await _masterContext.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound("Firma bulunamadı.");

        if (string.IsNullOrWhiteSpace(dto.NewPassword)) 
            return BadRequest("Yeni şifre boş olamaz.");

        var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        try
        {
            // 1. Update Tenant PasswordHash in Master DB
            tenant.PasswordHash = newHashedPassword;

            // 2. Clear confusion: Find the specific USER in Master DB and update them too
            var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email.ToLower() == tenant.Email.ToLower());
            if (user != null)
            {
                user.PasswordHash = newHashedPassword;
            }

            await _masterContext.SaveChangesAsync();

            _logger.LogInformation("Firma '{Name}' (Owner: {Email}) şifresi SuperAdmin tarafından sıfırlandı.", tenant.Name, tenant.Email);
            return Ok(new { Message = "Firmanın şifresi başarıyla güncellendi!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şifre resetleme hatası");
            return BadRequest(new { Error = ex.Message });
        }
    }
}

public class ResetTenantPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}
