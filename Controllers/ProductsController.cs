using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Entities;

namespace MultiTenantSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/products
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _context.Products.OrderBy(p => p.Name).ToListAsync();
        return Ok(products);
    }

    // GET /api/products/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Ürün bulunamadı." });
        return Ok(product);
    }

    // POST /api/products  (Yeni ürün ekle)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        product.Id = Guid.NewGuid();
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // PUT /api/products/{id}  (Ürün güncelle)
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Product updated)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Ürün bulunamadı." });

        product.Name = updated.Name;
        product.Type = updated.Type;
        product.Price = updated.Price;
        product.DepositPrice = updated.DepositPrice;
        product.Stock = updated.Stock;
        product.LinkedDepositId = updated.LinkedDepositId;

        await _context.SaveChangesAsync();
        return Ok(product);
    }

    // PATCH /api/products/{id}/stock  (Sadece stok güncelle)
    [HttpPatch("{id}/stock")]
    public async Task<IActionResult> UpdateStock(Guid id, [FromBody] StockUpdateDto dto)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Ürün bulunamadı." });

        product.Stock = dto.Stock;
        await _context.SaveChangesAsync();
        return Ok(product);
    }

    // DELETE /api/products/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Ürün bulunamadı." });

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record StockUpdateDto(int Stock);
