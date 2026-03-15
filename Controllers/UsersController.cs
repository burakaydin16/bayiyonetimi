using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;
using MultiTenantSaaS.Services;
using System.Security.Claims;

namespace MultiTenantSaaS.Controllers;

public record CreateUserDto(string Email, string Password, string Role, string Permissions);
public record UpdateUserPermissionsDto(string Permissions);

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin,SuperAdmin")] // Only Admins (or Super Admin via login-as) can manage users
public class UsersController : ControllerBase
{
    private readonly MasterDbContext _masterContext;
    private readonly ITenantService _tenantService;

    public UsersController(MasterDbContext masterContext, ITenantService tenantService)
    {
        _masterContext = masterContext;
        _tenantService = tenantService;
    }

    private Guid GetTenantId()
    {
        var tid = User.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value;
        if (Guid.TryParse(tid, out Guid tenantId)) return tenantId;
        return Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var tenantId = GetTenantId();
        var users = await _masterContext.Users
            .Where(u => u.TenantId == tenantId)
            .Select(u => new { u.Id, u.Email, u.Role, u.Permissions })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized("Tenant information missing.");

        if (await _masterContext.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
        {
            return BadRequest(new { Message = "Bu e-posta adresi ile zaten bir kullanıcı mevcut." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            Permissions = dto.Permissions
        };

        _masterContext.Users.Add(user);
        await _masterContext.SaveChangesAsync();

        return Ok(new { user.Id, user.Email, user.Role, user.Permissions });
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdateUserPermissionsDto dto)
    {
        var tenantId = GetTenantId();
        var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);
        if (user == null) return NotFound();

        user.Permissions = dto.Permissions;
        await _masterContext.SaveChangesAsync();

        return Ok(new { Message = "Yetkiler güncellendi." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var tenantId = GetTenantId();
        var user = await _masterContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);
        if (user == null) return NotFound();

        // Don't allow deleting yourself
        var currentUserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId == id.ToString())
        {
            return BadRequest(new { Message = "Kendi hesabınızı silemezsiniz." });
        }

        _masterContext.Users.Remove(user);
        await _masterContext.SaveChangesAsync();

        return Ok(new { Message = "Kullanıcı silindi." });
    }
}
