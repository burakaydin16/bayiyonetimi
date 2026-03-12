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
    }
}
