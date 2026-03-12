using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/customers  (Tüm müşteriler + depozito bakiyeleri)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _context.Customers.OrderBy(c => c.Name).ToListAsync();

        var customerIds = customers.Select(c => c.Id).ToList();
        var depositLedgers = await _context.DepositLedgers
            .Where(dl => customerIds.Contains(dl.CustomerId))
            .ToListAsync();

        var result = customers.Select(c => new
        {
            c.Id,
            c.Name,
            c.Type,
            c.Phone,
            c.Address,
            CashBalance = c.CashBalance,
            DepositBalances = depositLedgers
                .Where(dl => dl.CustomerId == c.Id)
                .ToDictionary(dl => dl.ProductId.ToString(), dl => dl.Balance)
        });

        return Ok(result);
    }

    // GET /api/customers/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = "Müşteri bulunamadı." });

        var deposits = await _context.DepositLedgers
            .Where(dl => dl.CustomerId == id)
            .ToListAsync();

        return Ok(new
        {
            customer.Id,
            customer.Name,
            customer.Type,
            customer.Phone,
            customer.Address,
            CashBalance = customer.CashBalance,
            DepositBalances = deposits.ToDictionary(dl => dl.ProductId.ToString(), dl => dl.Balance)
        });
    }

    // POST /api/customers  (Yeni müşteri ekle)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            Phone = dto.Phone,
            Address = dto.Address,
            CashBalance = dto.CashBalance
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    // PUT /api/customers/{id}  (Müşteri güncelle)
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerDto dto)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = "Müşteri bulunamadı." });

        customer.Name = dto.Name;
        customer.Type = dto.Type;
        customer.Phone = dto.Phone;
        customer.Address = dto.Address;
        customer.CashBalance = dto.CashBalance;

        await _context.SaveChangesAsync();
        return Ok(customer);
    }

    // DELETE /api/customers/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = "Müşteri bulunamadı." });

        // Depozito defterini de sil
        var deposits = _context.DepositLedgers.Where(dl => dl.CustomerId == id);
        _context.DepositLedgers.RemoveRange(deposits);

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/customers/{id}/deposit-ledger
    [HttpGet("{id}/deposit-ledger")]
    public async Task<IActionResult> GetDepositLedger(Guid id)
    {
        var deposits = await _context.DepositLedgers
            .Where(dl => dl.CustomerId == id)
            .ToListAsync();
        return Ok(deposits);
    }
}

public record CustomerDto(
    string Name,
    string Type,
    string? Phone,
    string? Address,
    decimal CashBalance
);
