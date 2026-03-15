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
    private readonly AppDbContext _appContext;
    private readonly ITenantService _tenantService;

    public UsersController(AppDbContext appContext, ITenantService tenantService)
    {
        _appContext = appContext;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _appContext.Users
            .Select(u => new { u.Id, u.Email, u.Role, u.Permissions })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        if (await _appContext.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new { Message = "Bu e-posta adresi ile zaten bir kullanıcı mevcut." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            Permissions = dto.Permissions
        };

        _appContext.Users.Add(user);
        await _appContext.SaveChangesAsync();

        return Ok(new { user.Id, user.Email, user.Role, user.Permissions });
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdateUserPermissionsDto dto)
    {
        var user = await _appContext.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Prevent changing own permissions if you are the only admin? 
        // For now, allow it.
        
        user.Permissions = dto.Permissions;
        await _appContext.SaveChangesAsync();

        return Ok(new { Message = "Yetkiler güncellendi." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _appContext.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Don't allow deleting yourself
        var currentUserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId == id.ToString())
        {
            return BadRequest(new { Message = "Kendi hesabınızı silemezsiniz." });
        }

        _appContext.Users.Remove(user);
        await _appContext.SaveChangesAsync();

        return Ok(new { Message = "Kullanıcı silindi." });
    }
}
