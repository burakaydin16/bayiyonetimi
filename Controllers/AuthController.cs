using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;
using MultiTenantSaaS.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;

namespace MultiTenantSaaS.Controllers;

public record LoginDto(string TenantRef, string Email, string Password);
public record SuperAdminLoginDto(string Username, string Password);
public record RegisterTenantDto(string Name, string Username, string Email, string Password);
public record ChangePasswordDto(string OldPassword, string NewPassword);

[ApiController]
[Route("api/auth")]
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

        // Basic email validation
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email))
        {
            return BadRequest(new { Message = "Lütfen geçerli bir e-posta adresi giriniz." });
        }

        // Check if email already exists
        var existingTenant = await _masterContext.Tenants.FirstOrDefaultAsync(t => t.Email == dto.Email);
        if (existingTenant != null)
        {
            return BadRequest(new { Message = "Bu e-posta adresi ile zaten kayıtlı bir firma var." });
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // Generate a sequential 6-digit Reference Code starting from 100000
        var lastTenant = await _masterContext.Tenants
            .OrderByDescending(t => t.ReferenceCode)
            .FirstOrDefaultAsync();

        int nextNum = 100000;
        if (lastTenant != null)
        {
            // Try to extract the numeric part from the last code (handles "REF-123" or just "123")
            var match = System.Text.RegularExpressions.Regex.Match(lastTenant.ReferenceCode, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int lastNum))
            {
                nextNum = Math.Max(100000, lastNum + 1);
            }
        }
        var refCode = nextNum.ToString();

        // 1. Create Tenant in Master DB (Pending Approval)
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = hashedPassword,
            SchemaName = $"tenant_{Guid.NewGuid().ToString("N")}",
            ReferenceCode = refCode,
            IsApproved = false // MUST be approved by Super Admin
        };

        try
        {
            _logger.LogInformation("Saving Tenant to Master DB (Pending Approval)...");
            _masterContext.Tenants.Add(tenant);
            await _masterContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register tenant");
            return BadRequest(new { Error = "Kayıt işlemi sırasında bir hata oluştu." });
        }

        _logger.LogInformation("RegisterTenant pending approval for {Email}", dto.Email);
        return Ok(new { Message = "Kayıt başarılı. Sistem yöneticisinin onayından sonra giriş yapabilirsiniz.", ReferenceCode = refCode });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // 1. Find Tenant by Reference Code
        var tenant = await _masterContext.Tenants.FirstOrDefaultAsync(t => t.ReferenceCode == dto.TenantRef);
        if (tenant == null) return Unauthorized("Firma bulunamadı. Lütfen geçerli bir Firma ID (Referans) giriniz.");

        if (!tenant.IsApproved) return Unauthorized("Firma kaydınız henüz sistem yöneticisi tarafından onaylanmamış.");
        
        // Explicitly set the schema context for this connection
        var connection = _appContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SET search_path TO \"{tenant.SchemaName}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        // Also set for interceptor usage in any subsequent EF calls (like SaveChanges)
        _tenantService.CurrentTenant = tenant;

        // 3. Find User in Tenant Schema
        _logger.LogInformation("Attempting login for user {Email} in schema {Schema}", dto.Email, tenant.SchemaName);
        var user = await _appContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());
        if (user == null) return Unauthorized("Invalid credentials");

        bool isPasswordValid = false;
        try 
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        }
        catch 
        {
            // Fallback for old unhashed passwords
            isPasswordValid = (user.PasswordHash == dto.Password);
            
            // Auto-migrate to hash here if it was valid? Optional, but good practice.
            if (isPasswordValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                await _appContext.SaveChangesAsync();
            }
        }

        if (!isPasswordValid) return Unauthorized("Invalid credentials");

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

        return Ok(new { 
            Token = tokenString, 
            User = new { user.Id, user.Email, user.Role, user.Permissions },
            TenantName = tenant.Name,
            TenantRef = tenant.ReferenceCode
        });
    }

    [HttpPost("super-admin-login")]
    public async Task<IActionResult> SuperAdminLogin(SuperAdminLoginDto dto)
    {
        var admin = await _masterContext.SuperAdmins.FirstOrDefaultAsync(a => a.Username == dto.Username || a.Email == dto.Username);
        
        // Setup initial default super admin if none exists 
        if (admin == null && dto.Username == "superadmin" && dto.Password == "nisan2023!")
        {
            admin = new SuperAdmin {
                Id = Guid.NewGuid(),
                Username = "superadmin",
                Email = "admin@nisanaydin.com.tr",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("nisan2023!")
            };
            _masterContext.SuperAdmins.Add(admin);
            await _masterContext.SaveChangesAsync();
        }
        else if (admin == null)
        {
            return Unauthorized("Geçersiz süper admin bilgileri.");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, admin.PasswordHash))
        {
            return Unauthorized("Geçersiz süper admin bilgileri.");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "super_secret_key_1234567890123456");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Email, admin.Email),
                new Claim(ClaimTypes.Role, "SuperAdmin")
            }),
            Expires = DateTime.UtcNow.AddDays(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new { Token = tokenString });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (string.IsNullOrEmpty(dto.OldPassword) || string.IsNullOrEmpty(dto.NewPassword))
        {
            return BadRequest(new { Message = "Old and new passwords are required." });
        }

        // 1. Get logged in user's ID from JWT Claims
        var userIdString = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized("Invalid user token");
        }

        // 2. Find User in DB
        var user = await _appContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // 3. Verify Old Password safely
        bool isOldPasswordValid = false;
        try 
        {
            isOldPasswordValid = BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash);
        } 
        catch 
        {
            // If the database has an old plaintext password, BCrypt throws an exception
            isOldPasswordValid = (user.PasswordHash == dto.OldPassword);
        }

        if (!isOldPasswordValid)
        {
            return BadRequest(new { Message = "Incorrect old password." });
        }

        // 4. Update with New Password (Hashed)
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        
        await _appContext.SaveChangesAsync();

        return Ok(new { Message = "Password changed successfully." });
    }
}
