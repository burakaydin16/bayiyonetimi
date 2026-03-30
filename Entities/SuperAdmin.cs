using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Entities;

public class SuperAdmin
{
    [Key]
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = "bayiyonetimi016@gmail.com";
    public string PasswordHash { get; set; } = string.Empty;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
}
