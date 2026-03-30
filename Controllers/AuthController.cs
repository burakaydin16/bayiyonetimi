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
public record UpdateLogoDto(string LogoUrl);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MasterDbContext _masterContext;
    private readonly AppDbContext _appContext;
    private readonly IConfiguration _configuration;
    private readonly ITenantService _tenantService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(MasterDbContext masterContext, AppDbContext appContext, IConfiguration configuration, ITenantService tenantService, IEmailService emailService, ILogger<AuthController> logger)
    {
        _masterContext = masterContext;
        _appContext = appContext;
        _configuration = configuration;
        _tenantService = tenantService;
        _emailService = emailService;
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
        var tenantId = Guid.NewGuid();
        var verificationToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = hashedPassword,
            SchemaName = $"tenant_{Guid.NewGuid().ToString("N")}",
            ReferenceCode = refCode,
            IsApproved = false // MUST be approved by Super Admin
        };

        // 2. Create the first Admin User in Master DB (Centralized)
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = dto.Email,
            PasswordHash = hashedPassword,
            Role = "Admin",
            Permissions = "*",
            IsEmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        try
        {
            _logger.LogInformation("Saving Tenant and User to Master DB (Pending Email Verification)...");
            _masterContext.Tenants.Add(tenant);
            _masterContext.Users.Add(user);
            await _masterContext.SaveChangesAsync();

            // 3. Send Verification Email (Not Admin notification yet!)
            var origin = Request.Headers["Origin"].ToString();
            if (string.IsNullOrEmpty(origin)) origin = "http://localhost:5173"; // fallback
            var verificationLink = $"{origin}/verify-email?token={verificationToken}";

            await _emailService.SendEmailVerificationAsync(user.Email, tenant.Name, verificationLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register tenant");
            return BadRequest(new { Error = "Kayıt işlemi sırasında bir hata oluştu." });
        }

        _logger.LogInformation("RegisterTenant pending email verification for {Email}", dto.Email);
        return Ok(new { Message = "Kayıt başarılı. Lütfen e-posta adresinize gönderilen doğrulama bağlantısına tıklayınız.", ReferenceCode = refCode });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrEmpty(token)) return BadRequest("Token bulunamadı.");

        var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        if (user == null) return BadRequest("Geçersiz doğrulama bağlantısı.");

        if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
            return BadRequest("Doğrulama bağlantısının süresi dolmuş. Lütfen tekrar kayıt olun veya destek ile iletişime geçin.");

        var tenant = await _masterContext.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
        if (tenant == null) return BadRequest("İlgili firma bulunamadı.");

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;

        await _masterContext.SaveChangesAsync();

        // 4. NOW notify the SuperAdmin that the email was verified and it is ready for approval
        await _emailService.SendNewRegistrationNotificationAsync(tenant.Name, tenant.Email);

        return Ok(new { Message = "E-posta başarıyla doğrulandı. Üyeliğiniz sistem yöneticisi onayına sunulmuştur." });
    }

    public record ForgotPasswordDto(string Email);

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("E-posta adresi gereklidir.");

        var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());
        if (user == null) return Ok(new { Message = "Eğer bu e-posta sistemde kayıtlıysa, parola sıfırlama linki gönderildi." }); // Security best practice

        var resetToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(24);
        
        await _masterContext.SaveChangesAsync();

        var origin = Request.Headers["Origin"].ToString();
        if (string.IsNullOrEmpty(origin)) origin = "http://localhost:5173"; // fallback
        var resetLink = $"{origin}/reset-password?token={resetToken}";

        await _emailService.SendPasswordResetAsync(user.Email, resetLink);

        return Ok(new { Message = "Eğer bu e-posta sistemde kayıtlıysa, parola sıfırlama linki gönderildi." });
    }

    public record ResetPasswordDto(string Token, string NewPassword);

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.NewPassword)) 
            return BadRequest("Token ve yeni şifre zorunludur.");

        var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == dto.Token);
        if (user == null) return BadRequest("Geçersiz veya süresi dolmuş bağlantı.");

        if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            return BadRequest("Bağlantının süresi dolmuş. Lütfen yenisini talep ediniz.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        await _masterContext.SaveChangesAsync();

        return Ok(new { Message = "Şifreniz başarıyla değiştirildi. Şimdi giriş yapabilirsiniz." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // 1. Find User in Master DB (Centralized)
        // This is always in 'public' so it's super fast and stable
        var user = await _masterContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

        if (user == null) 
        {
            return Unauthorized("Kullanıcı bulunamadı. Lütfen e-posta adresinizi kontrol edin.");
        }

        // 2. Find associated Tenant
        var tenant = await _masterContext.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
        if (tenant == null) return Unauthorized("İlişkili firma kaydı bulunamadı.");

        // Check if Tenant ID matches the one provided in Login (security check)
        if (tenant.ReferenceCode != dto.TenantRef)
        {
            return Unauthorized("Girdiğiniz firma ID'si (Referans) bu kullanıcıya ait değil.");
        }

        if (!tenant.IsApproved) return Unauthorized("Firma kaydınız henüz sistem yöneticisi tarafından onaylanmamış.");
        if (!tenant.IsActive) return Unauthorized("Firma hesabınız askıya alınmıştır. Lütfen sistem yöneticisi ile iletişime geçin.");

        _logger.LogInformation("User {Email} found for tenant {Name}. Verifying password...", dto.Email, tenant.Name);

        bool isPasswordValid = false;
        try 
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BCrypt verification error, trying fallback");
            isPasswordValid = (user.PasswordHash == dto.Password);
            
            if (isPasswordValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                await _masterContext.SaveChangesAsync();
            }
        }

        if (!isPasswordValid) 
        {
            return Unauthorized("Şifre hatalı.");
        }

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
            TenantRef = tenant.ReferenceCode,
            LogoUrl = tenant.LogoUrl
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

        // 2. Find User in Master DB
        var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
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
            isOldPasswordValid = (user.PasswordHash == dto.OldPassword);
        }

        if (!isOldPasswordValid)
        {
            return BadRequest(new { Message = "Incorrect old password." });
        }

        // 4. Update with New Password (Hashed)
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        
        await _masterContext.SaveChangesAsync();

        return Ok(new { Message = "Password changed successfully." });
    }

    [Authorize]
    [HttpPost("update-logo")]
    public async Task<IActionResult> UpdateLogo(UpdateLogoDto dto)
    {
        var tenantIdString = User.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantIdString) || !Guid.TryParse(tenantIdString, out Guid tenantId))
        {
            return Unauthorized("Invalid tenant token");
        }

        var tenant = await _masterContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null)
        {
            return NotFound("Tenant not found");
        }

        tenant.LogoUrl = dto.LogoUrl;
        await _masterContext.SaveChangesAsync();

        return Ok(new { Message = "Logo updated successfully.", LogoUrl = tenant.LogoUrl });
    }
}
