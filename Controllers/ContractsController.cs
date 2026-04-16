using Microsoft.AspNetCore.Mvc;
using InstallmentSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models;
using InstallmentSystem.Services;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IContractService _service;

    public ContractsController(AppDbContext db, IContractService service)
    {
        _db = db;
        _service = service;
    }

    [HttpGet]
    [HasPermission("Contract.Read")]
    public async Task<IActionResult> GetAll([FromQuery] ContractStatus? status, [FromQuery] Guid? customerId)
    {
        var query = _db.InstallmentContracts
            .Include(c => c.Customer)
            .Include(c => c.Currency)
            .Include(c => c.Installments)
            .AsQueryable();

        if (status.HasValue) query = query.Where(c => c.Status == status);
        if (customerId.HasValue)           query = query.Where(c => c.CustomerId == customerId);

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new {
                c.Id, c.ContractNumber, c.ContractDate, c.TotalAmount,
                c.TotalAmountInBase, c.DownPayment, c.RemainingAmount,
                c.InstallmentCount, c.InstallmentValue, c.ExchangeRate,
                c.Status, c.CreatedAt,
                CustomerName   = c.Customer.FullName,
                CurrencyCode   = c.Currency.Code,
                CurrencySymbol = c.Currency.Symbol,
                PaidInstallments    = c.Installments.Count(i => i.Status == InstallmentStatus.Paid),
                OverdueInstallments = c.Installments.Count(i => i.Status == InstallmentStatus.Overdue)
            }).ToListAsync();

        return Ok(contracts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var contract = await _db.InstallmentContracts
            .Include(c => c.Customer)
            .Include(c => c.Currency)
            .Include(c => c.Installments)
            .Include(c => c.ContractItems).ThenInclude(ci => ci.Product)
            .Include(c => c.Payments).ThenInclude(p => p.Receipt).ThenInclude(r => r!.Currency)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null) return NotFound();
        return Ok(contract);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContractDto dto)
    {
        try
        {
            var contract = await _service.CreateContractAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = contract.Id }, contract);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractDto dto)
    {
        var contract = await _db.InstallmentContracts
            .Include(c => c.Installments)
            .Include(c => c.ContractItems)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null) return NotFound();
        if (contract.Status != ContractStatus.Active)
            return BadRequest(new { message = "لا يمكن تعديل عقد غير نشط" });

        var currency = await _db.Currencies.FindAsync(dto.CurrencyId)
            ?? await _db.Currencies.FirstAsync(c => c.IsBase);

        // احتساب المجموع من المواد إذا وجدت
        var totalFromItems = dto.Items.Sum(i => i.Quantity * i.Price);
        var totalAmount    = totalFromItems > 0 ? totalFromItems : dto.TotalAmount;
        var remaining      = totalAmount - dto.DownPayment;
        var installmentVal = dto.InstallmentCount > 0 ? remaining / dto.InstallmentCount : 0;

        contract.CustomerId       = dto.CustomerId;
        contract.CurrencyId       = dto.CurrencyId;
        contract.ExchangeRate     = currency.ExchangeRate;
        contract.ContractDate     = dto.ContractDate;
        contract.TotalAmount      = totalAmount;
        contract.TotalAmountInBase= totalAmount * currency.ExchangeRate;
        contract.DownPayment      = dto.DownPayment;
        contract.RemainingAmount  = remaining;
        contract.InstallmentCount = dto.InstallmentCount;
        contract.InstallmentValue = installmentVal;

        // إعادة حساب الأقساط الغير مدفوعة
        foreach (var inst in contract.Installments.Where(i => i.Status != InstallmentStatus.Paid))
        {
            inst.Amount          = installmentVal;
            inst.RemainingAmount = installmentVal - inst.PaidAmount;
        }

        // تحديث المنتجات
        _db.ContractItems.RemoveRange(contract.ContractItems);
        foreach (var item in dto.Items)
        {
            contract.ContractItems.Add(new ContractItem
            {
                ContractId = contract.Id,
                ProductId  = item.ProductId,
                Quantity   = item.Quantity,
                Price      = item.Price
            });
        }

        await _db.SaveChangesAsync();
        return Ok(contract);
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var contract = await _db.InstallmentContracts.FindAsync(id);
        if (contract == null) return NotFound();
        contract.Status = ContractStatus.Cancelled;
        await _db.SaveChangesAsync();
        return Ok(new { message = "تم إلغاء العقد" });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var stats = new
        {
            TotalContracts      = await _db.InstallmentContracts.CountAsync(),
            ActiveContracts     = await _db.InstallmentContracts.CountAsync(c => c.Status == ContractStatus.Active),
            CompletedContracts  = await _db.InstallmentContracts.CountAsync(c => c.Status == ContractStatus.Completed),
            TotalRevenue        = await _db.Payments.Where(p => !p.IsCancelled).SumAsync(p => (decimal?)p.Amount) ?? 0,
            TotalCustomers      = await _db.Customers.CountAsync(),
            OverdueInstallments = await _db.Installments.CountAsync(i => i.Status == InstallmentStatus.Overdue),
            PendingInstallments = await _db.Installments.CountAsync(i => i.Status == InstallmentStatus.Pending),
            TodayCollections    = await _db.Payments
                .Where(p => !p.IsCancelled && p.PaymentDate.Date == DateTime.UtcNow.Date)
                .SumAsync(p => (decimal?)p.Amount) ?? 0
        };
        return Ok(stats);
    }

    [HttpDelete("{id}")]
    [HasPermission("Contract.Delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteContractAsync(id);
            return Ok(new { message = "تم حذف العقد وكافة بياناته بنجاح" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
