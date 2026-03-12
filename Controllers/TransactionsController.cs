using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TransactionsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/transactions  (Tüm hareketler + kalemler)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var transactions = await _context.Transactions
            .Include(t => t.Items)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
        return Ok(transactions);
    }

    // GET /api/transactions/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (transaction == null) return NotFound(new { message = "Hareket bulunamadı." });
        return Ok(transaction);
    }

    // GET /api/transactions/customer/{customerId}  (Müşteriye ait hareketler)
    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId)
    {
        var transactions = await _context.Transactions
            .Include(t => t.Items)
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
        return Ok(transactions);
    }

    // POST /api/transactions/process  (Hareket kaydet + stok/bakiye/depozito güncelle)
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] TransactionProcessDto dto)
    {
        // 1. Hareket başlığını oluştur
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Date = dto.Date ?? DateTime.UtcNow,
            CustomerId = dto.CustomerId,
            TotalAmount = dto.Items.Sum(i => i.Quantity * i.UnitPrice),
            Notes = dto.Notes,
            Type = dto.Type
        };

        // 2. Kalemleri işle
        foreach (var itemDto in dto.Items)
        {
            var item = new TransactionItem
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                ProductId = itemDto.ProductId,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                ItemType = itemDto.ItemType
            };
            transaction.Items.Add(item);

            // 3. Stok güncelle
            var product = await _context.Products.FindAsync(itemDto.ProductId);
            if (product != null)
            {
                switch (itemDto.ItemType)
                {
                    case "Gonderilen":
                        product.Stock -= itemDto.Quantity;
                        break;
                    case "IadeAlinan":
                        product.Stock += itemDto.Quantity;
                        break;
                    case "StokGirisi":
                        product.Stock += itemDto.Quantity;
                        break;
                }

                // 4. Depozito defteri güncelle (eğer depozitolu ürünse ve müşteri varsa)
                if (dto.CustomerId.HasValue && product.LinkedDepositId.HasValue)
                {
                    var depositProductId = product.LinkedDepositId.Value;
                    var ledger = await _context.DepositLedgers
                        .FirstOrDefaultAsync(dl => dl.CustomerId == dto.CustomerId.Value && dl.ProductId == depositProductId);

                    if (ledger == null)
                    {
                        ledger = new DepositLedger { CustomerId = dto.CustomerId.Value, ProductId = depositProductId, Balance = 0 };
                        _context.DepositLedgers.Add(ledger);
                    }

                    switch (itemDto.ItemType)
                    {
                        case "Gonderilen":
                            ledger.Balance += itemDto.Quantity; // Müşteride damacana artar
                            break;
                        case "IadeAlinan":
                            ledger.Balance -= itemDto.Quantity; // Müşteride damacana azalır
                            break;
                    }
                }
            }
        }

        // 5. Müşteri nakit bakiye güncelle
        if (dto.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(dto.CustomerId.Value);
            if (customer != null && dto.Type == "CustomerOp")
            {
                customer.CashBalance -= transaction.TotalAmount; // Borç artar (bakiye düşer)
            }
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
    }

    // DELETE /api/transactions/{id}  (Hareketi iptal et, stok/bakiye geri al)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null) return NotFound(new { message = "Hareket bulunamadı." });

        // Stok ve bakiyeleri tersine çevir
        foreach (var item in transaction.Items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                switch (item.ItemType)
                {
                    case "Gonderilen":
                        product.Stock += item.Quantity; // Geri al
                        break;
                    case "IadeAlinan":
                        product.Stock -= item.Quantity;
                        break;
                    case "StokGirisi":
                        product.Stock -= item.Quantity;
                        break;
                }

                // Depozito ters
                if (transaction.CustomerId.HasValue && product.LinkedDepositId.HasValue)
                {
                    var depositProductId = product.LinkedDepositId.Value;
                    var ledger = await _context.DepositLedgers
                        .FirstOrDefaultAsync(dl => dl.CustomerId == transaction.CustomerId.Value && dl.ProductId == depositProductId);
                    if (ledger != null)
                    {
                        if (item.ItemType == "Gonderilen") ledger.Balance -= item.Quantity;
                        if (item.ItemType == "IadeAlinan") ledger.Balance += item.Quantity;
                    }
                }
            }
        }

        // Müşteri bakiye geri al
        if (transaction.CustomerId.HasValue && transaction.Type == "CustomerOp")
        {
            var customer = await _context.Customers.FindAsync(transaction.CustomerId.Value);
            if (customer != null) customer.CashBalance += transaction.TotalAmount;
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

// --- DTOs ---
public class TransactionProcessDto
{
    public Guid? CustomerId { get; set; }
    public DateTime? Date { get; set; }
    public string? Notes { get; set; }
    public string Type { get; set; } = "CustomerOp";
    public List<TransactionItemDto> Items { get; set; } = new();
}

public class TransactionItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ItemType { get; set; } = string.Empty; // Gonderilen | IadeAlinan | StokGirisi
}
