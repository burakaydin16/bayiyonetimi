using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Entities;

public class SuperAdmin
{
    [Key]
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
