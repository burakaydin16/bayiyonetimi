
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MultiTenantSaaS.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class Product
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal DepositPrice { get; set; }
    public int Stock { get; set; }
    public Guid? LinkedDepositId { get; set; }
}

public class Customer
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Dealer";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal CashBalance { get; set; }
}

public class Transaction
{
    [Key]
    public Guid Id { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public Guid? CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string Type { get; set; } = string.Empty;
    
    public List<TransactionItem> Items { get; set; } = new();
}

public class TransactionItem
{
    [Key]
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ItemType { get; set; } = string.Empty;
}

public class DepositLedger
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public int Balance { get; set; }
}
