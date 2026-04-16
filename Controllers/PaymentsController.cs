using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Services;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IContractService _service;

    public PaymentsController(AppDbContext db, IContractService service)
    {
        _db = db;
        _service = service;
    }

    [HttpGet]
    [HasPermission("Payment.Read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? cancelled)
    {
        var query = _db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Contract)
            .Include(p => p.Receipt).ThenInclude(r => r!.Currency)
            .AsQueryable();

        if (from.HasValue)      query = query.Where(p => p.PaymentDate >= from.Value);
        if (to.HasValue)        query = query.Where(p => p.PaymentDate <= to.Value);
        if (cancelled.HasValue) query = query.Where(p => p.IsCancelled == cancelled.Value);

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new {
                p.Id, p.Amount, p.PaymentDate, p.PaymentMethod,
                p.Notes, p.IsCancelled, p.CancelledAt, p.CancelReason,
                CustomerName   = p.Customer.FullName,
                ContractNumber = p.Contract.ContractNumber,
                ReceiptId      = p.Receipt != null ? p.Receipt.Id : (Guid?)null,
                ReceiptNumber  = p.Receipt != null ? p.Receipt.ReceiptNumber : null,
                CurrencySymbol = p.Receipt != null ? p.Receipt.Currency.Symbol : "د.ع"
            }).ToListAsync();

        return Ok(payments);
    }

    [HttpGet("contract/{contractId}")]
    [HasPermission("Payment.Read")]
    public async Task<IActionResult> GetByContract(Guid contractId)
    {
        var payments = await _db.Payments
            .Include(p => p.Receipt).ThenInclude(r => r!.Currency)
            .Where(p => p.ContractId == contractId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new {
                p.Id, p.Amount, p.PaymentDate, p.PaymentMethod,
                p.Notes, p.IsCancelled, p.CancelledAt,
                ReceiptNumber  = p.Receipt != null ? p.Receipt.ReceiptNumber : null,
                CurrencySymbol = p.Receipt != null ? p.Receipt.Currency.Symbol : "د.ع"
            }).ToListAsync();

        return Ok(payments);
    }

    [HttpPost]
    [HasPermission("Payment.Create")]
    public async Task<IActionResult> Pay([FromBody] CreatePaymentDto dto)
    {
        try
        {
            var payment = await _service.ProcessPaymentAsync(dto);
            // Return payment with receipt info
            var result = await _db.Payments
                .Include(p => p.Receipt)
                .FirstOrDefaultAsync(p => p.Id == payment.Id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelPaymentDto dto)
    {
        try
        {
            await _service.CancelPaymentAsync(id, dto.CancelReason);
            return Ok(new { message = "تم إلغاء الدفعة وسند القبض والقيد المحاسبي بنجاح" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
