using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Entities;

public class Tenant
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty; // e.g., "tenant_abc"
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Simplified for demo
}
