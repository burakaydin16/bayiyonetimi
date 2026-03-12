
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionItem> TransactionItems { get; set; }
    public DbSet<DepositLedger> DepositLedgers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table names mapping (matching the SQL in README.md)
        modelBuilder.Entity<User>().ToTable("users");
        
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Product>().Property(p => p.Price).HasColumnName("price");
        modelBuilder.Entity<Product>().Property(p => p.Stock).HasColumnName("stock");
        modelBuilder.Entity<Product>().Property(p => p.DepositPrice).HasColumnName("deposit_price");
        modelBuilder.Entity<Product>().Property(p => p.LinkedDepositId).HasColumnName("linked_deposit_id");

        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Customer>().Property(c => c.CashBalance).HasColumnName("cash_balance");

        modelBuilder.Entity<Transaction>().ToTable("transactions");
        modelBuilder.Entity<Transaction>().Property(t => t.TotalAmount).HasColumnName("total_amount");
        modelBuilder.Entity<Transaction>().Property(t => t.CustomerId).HasColumnName("customer_id");

        modelBuilder.Entity<TransactionItem>().ToTable("transaction_items");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.TransactionId).HasColumnName("transaction_id");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.ProductId).HasColumnName("product_id");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.UnitPrice).HasColumnName("unit_price");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.ItemType).HasColumnName("item_type");

        modelBuilder.Entity<DepositLedger>().ToTable("deposit_ledgers");
        modelBuilder.Entity<DepositLedger>().HasKey(dl => new { dl.CustomerId, dl.ProductId });
        modelBuilder.Entity<DepositLedger>().Property(dl => dl.CustomerId).HasColumnName("customer_id");
        modelBuilder.Entity<DepositLedger>().Property(dl => dl.ProductId).HasColumnName("product_id");
        modelBuilder.Entity<DepositLedger>().Property(dl => dl.Balance).HasColumnName("balance");
    }
}
