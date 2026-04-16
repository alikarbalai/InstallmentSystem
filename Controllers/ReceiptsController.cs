using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Data;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReceiptsController(AppDbContext db) => _db = db;

    [HttpGet]
    [HasPermission("Receipt.Read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? cancelled)
    {
        var query = _db.Receipts
            .Include(r => r.Customer)
            .Include(r => r.Currency)
            .Include(r => r.Payment).ThenInclude(p => p.Contract)
            .AsQueryable();

        if (from.HasValue)      query = query.Where(r => r.ReceiptDate >= from.Value);
        if (to.HasValue)        query = query.Where(r => r.ReceiptDate <= to.Value);
        if (cancelled.HasValue) query = query.Where(r => r.IsCancelled == cancelled.Value);

        var receipts = await query
            .OrderByDescending(r => r.ReceiptDate)
            .Select(r => new {
                r.Id, r.ReceiptNumber, r.Amount, r.AmountInBase,
                r.ExchangeRate, r.PaymentMethod, r.Notes,
                r.ReceiptDate, r.IsCancelled, r.CancelledAt, r.CancelReason,
                CustomerName   = r.Customer.FullName,
                CustomerPhone  = r.Customer.Phone,
                CurrencyCode   = r.Currency.Code,
                CurrencySymbol = r.Currency.Symbol,
                ContractNumber = r.Payment.Contract.ContractNumber,
                PaymentId      = r.PaymentId
            }).ToListAsync();

        return Ok(receipts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var receipt = await _db.Receipts
            .Include(r => r.Customer)
            .Include(r => r.Currency)
            .Include(r => r.Payment).ThenInclude(p => p.Contract)
            .Include(r => r.JournalEntry).ThenInclude(j => j!.Details).ThenInclude(d => d.Account)
            .FirstOrDefaultAsync(r => r.Id == id);

        return receipt == null ? NotFound() : Ok(receipt);
    }

    [HttpGet("payment/{paymentId}")]
    public async Task<IActionResult> GetByPayment(Guid paymentId)
    {
        var receipt = await _db.Receipts
            .Include(r => r.Currency)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

        return receipt == null ? NotFound() : Ok(receipt);
    }
}
