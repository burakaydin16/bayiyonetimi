using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Data;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<SuperAdmin> SuperAdmins { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().ToTable("tenants", "public");
        modelBuilder.Entity<Tenant>().Property(t => t.Id).HasColumnName("id");
        modelBuilder.Entity<Tenant>().Property(t => t.Name).HasColumnName("name");
        modelBuilder.Entity<Tenant>().Property(t => t.Email).HasColumnName("email");
        modelBuilder.Entity<Tenant>().Property(t => t.PasswordHash).HasColumnName("password_hash");
        modelBuilder.Entity<Tenant>().Property(t => t.SchemaName).HasColumnName("schema_name");
        modelBuilder.Entity<Tenant>().Property(t => t.ReferenceCode).HasColumnName("reference_code");
        modelBuilder.Entity<Tenant>().Property(t => t.IsApproved).HasColumnName("is_approved");
        modelBuilder.Entity<Tenant>().Property(t => t.LogoUrl).HasColumnName("logo_url");

        modelBuilder.Entity<User>().ToTable("users", "public");
        modelBuilder.Entity<User>().Property(u => u.Id).HasColumnName("id");
        modelBuilder.Entity<User>().Property(u => u.TenantId).HasColumnName("tenant_id");
        modelBuilder.Entity<User>().Property(u => u.Email).HasColumnName("email");
        modelBuilder.Entity<User>().Property(u => u.PasswordHash).HasColumnName("password_hash");
        modelBuilder.Entity<User>().Property(u => u.Role).HasColumnName("role");
        modelBuilder.Entity<User>().Property(u => u.Permissions).HasColumnName("permissions");

        modelBuilder.Entity<User>().Property(u => u.IsEmailVerified).HasColumnName("is_email_verified");
        modelBuilder.Entity<User>().Property(u => u.EmailVerificationToken).HasColumnName("email_verification_token");
        modelBuilder.Entity<User>().Property(u => u.EmailVerificationTokenExpiry).HasColumnName("email_verification_token_expiry");
        modelBuilder.Entity<User>().Property(u => u.PasswordResetToken).HasColumnName("password_reset_token");
        modelBuilder.Entity<User>().Property(u => u.PasswordResetTokenExpiry).HasColumnName("password_reset_token_expiry");

        modelBuilder.Entity<SuperAdmin>().ToTable("super_admins", "public");
        modelBuilder.Entity<SuperAdmin>().Property(s => s.Id).HasColumnName("id");
        modelBuilder.Entity<SuperAdmin>().Property(s => s.Username).HasColumnName("username");
        modelBuilder.Entity<SuperAdmin>().Property(s => s.Email).HasColumnName("email");
        modelBuilder.Entity<SuperAdmin>().Property(s => s.PasswordHash).HasColumnName("password_hash");
    }
}
