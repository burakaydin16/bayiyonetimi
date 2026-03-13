
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
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<User>().Property(u => u.Id).HasColumnName("id");
        modelBuilder.Entity<User>().Property(u => u.Email).HasColumnName("email");
        modelBuilder.Entity<User>().Property(u => u.PasswordHash).HasColumnName("password_hash");
        modelBuilder.Entity<User>().Property(u => u.Role).HasColumnName("role");
        
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Product>().Property(p => p.Id).HasColumnName("id");
        modelBuilder.Entity<Product>().Property(p => p.Name).HasColumnName("name");
        modelBuilder.Entity<Product>().Property(p => p.Type).HasColumnName("type");
        modelBuilder.Entity<Product>().Property(p => p.Price).HasColumnName("price");
        modelBuilder.Entity<Product>().Property(p => p.Stock).HasColumnName("stock");
        modelBuilder.Entity<Product>().Property(p => p.DepositPrice).HasColumnName("deposit_price");
        modelBuilder.Entity<Product>().Property(p => p.LinkedDepositId).HasColumnName("linked_deposit_id");

        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Customer>().Property(c => c.Id).HasColumnName("id");
        modelBuilder.Entity<Customer>().Property(c => c.Name).HasColumnName("name");
        modelBuilder.Entity<Customer>().Property(c => c.Type).HasColumnName("type");
        modelBuilder.Entity<Customer>().Property(c => c.Phone).HasColumnName("phone");
        modelBuilder.Entity<Customer>().Property(c => c.Address).HasColumnName("address");
        modelBuilder.Entity<Customer>().Property(c => c.CashBalance).HasColumnName("cash_balance");

        modelBuilder.Entity<Transaction>().ToTable("transactions");
        modelBuilder.Entity<Transaction>().Property(t => t.Id).HasColumnName("id");
        modelBuilder.Entity<Transaction>().Property(t => t.Date).HasColumnName("date");
        modelBuilder.Entity<Transaction>().Property(t => t.TotalAmount).HasColumnName("total_amount");
        modelBuilder.Entity<Transaction>().Property(t => t.CustomerId).HasColumnName("customer_id");
        modelBuilder.Entity<Transaction>().Property(t => t.Notes).HasColumnName("notes");
        modelBuilder.Entity<Transaction>().Property(t => t.Type).HasColumnName("type");

        modelBuilder.Entity<TransactionItem>().ToTable("transaction_items");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.Id).HasColumnName("id");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.TransactionId).HasColumnName("transaction_id");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.ProductId).HasColumnName("product_id");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.Quantity).HasColumnName("quantity");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.UnitPrice).HasColumnName("unit_price");
        modelBuilder.Entity<TransactionItem>().Property(ti => ti.ItemType).HasColumnName("item_type");

        modelBuilder.Entity<DepositLedger>().ToTable("deposit_ledgers");
        modelBuilder.Entity<DepositLedger>().HasKey(dl => new { dl.CustomerId, dl.ProductId });
        modelBuilder.Entity<DepositLedger>().Property(dl => dl.CustomerId).HasColumnName("customer_id");
        modelBuilder.Entity<DepositLedger>().Property(dl => dl.ProductId).HasColumnName("product_id");
        modelBuilder.Entity<DepositLedger>().Property(dl => dl.Balance).HasColumnName("balance");
    }
}
