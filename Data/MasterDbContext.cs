using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Data;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().ToTable("tenants", "public");
        modelBuilder.Entity<Tenant>().Property(t => t.Id).HasColumnName("id");
        modelBuilder.Entity<Tenant>().Property(t => t.Name).HasColumnName("name");
        modelBuilder.Entity<Tenant>().Property(t => t.Email).HasColumnName("email");
        modelBuilder.Entity<Tenant>().Property(t => t.PasswordHash).HasColumnName("password_hash");
        modelBuilder.Entity<Tenant>().Property(t => t.SchemaName).HasColumnName("schema_name");
    }
}
