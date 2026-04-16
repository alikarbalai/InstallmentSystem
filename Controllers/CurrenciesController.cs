using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CurrenciesController(AppDbContext db) => _db = db;

    [HttpGet]
    [HasPermission("Currency.Read")]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Currencies.OrderBy(c => c.IsBase ? 0 : 1).ToListAsync());

    [HttpGet("{id}")]
    [HasPermission("Currency.Read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await _db.Currencies.FindAsync(id);
        return c == null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCurrencyDto dto)
    {
        if (dto.IsBase && await _db.Currencies.AnyAsync(c => c.IsBase))
            return BadRequest(new { message = "توجد عملة أساسية بالفعل" });

        var currency = new Currency
        {
            Name         = dto.Name,
            Code         = dto.Code,
            Symbol       = dto.Symbol,
            ExchangeRate = dto.IsBase ? 1 : dto.ExchangeRate,
            IsBase       = dto.IsBase
        };
        _db.Currencies.Add(currency);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = currency.Id }, currency);
    }

    [HttpPatch("{id}/exchange-rate")]
    public async Task<IActionResult> UpdateRate(Guid id, [FromBody] UpdateExchangeRateDto dto)
    {
        var currency = await _db.Currencies.FindAsync(id);
        if (currency == null) return NotFound();
        if (currency.IsBase) return BadRequest(new { message = "لا يمكن تغيير سعر العملة الأساسية" });

        currency.ExchangeRate = dto.ExchangeRate;
        await _db.SaveChangesAsync();
        return Ok(currency);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currency = await _db.Currencies.FindAsync(id);
        if (currency == null) return NotFound();
        if (currency.IsBase) return BadRequest(new { message = "لا يمكن حذف العملة الأساسية" });

        _db.Currencies.Remove(currency);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
