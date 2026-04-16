using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Data;
using InstallmentSystem.Services;
using InstallmentSystem.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InstallmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IContractService _service;

    public InstallmentsController(AppDbContext db, IContractService service)
    {
        _db = db;
        _service = service;
    }

    [HttpGet("contract/{contractId}")]
    [HasPermission("Contract.Read")]
    public async Task<IActionResult> GetByContract(Guid contractId)
    {
        var installments = await _db.Installments
            .Where(i => i.ContractId == contractId)
            .OrderBy(i => i.InstallmentNumber)
            .ToListAsync();
        return Ok(installments);
    }

    [HttpGet("overdue")]
    [HasPermission("Contract.Read")]
    public async Task<IActionResult> GetOverdue()
    {
        var overdue = await _db.Installments
            .Include(i => i.Contract).ThenInclude(c => c.Customer)
            .Where(i => i.Status == InstallmentStatus.Overdue || (i.Status == InstallmentStatus.Pending && i.DueDate < DateTime.UtcNow))
            .OrderBy(i => i.DueDate)
            .Select(i => new {
                i.Id, i.InstallmentNumber, i.DueDate, i.Amount, i.RemainingAmount, i.Status,
                ContractNumber = i.Contract.ContractNumber,
                CustomerName = i.Contract.Customer.FullName,
                CustomerPhone = i.Contract.Customer.Phone
            })
            .ToListAsync();
        return Ok(overdue);
    }

    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming([FromQuery] int days = 7)
    {
        var until = DateTime.UtcNow.AddDays(days);
        var upcoming = await _db.Installments
            .Include(i => i.Contract).ThenInclude(c => c.Customer)
            .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate >= DateTime.UtcNow && i.DueDate <= until)
            .OrderBy(i => i.DueDate)
            .Select(i => new {
                i.Id, i.InstallmentNumber, i.DueDate, i.Amount, i.RemainingAmount,
                ContractNumber = i.Contract.ContractNumber,
                CustomerName = i.Contract.Customer.FullName,
                CustomerPhone = i.Contract.Customer.Phone
            })
            .ToListAsync();
        return Ok(upcoming);
    }

    [HttpPost("update-overdue")]
    [HasPermission("Contract.Update")]
    public async Task<IActionResult> UpdateOverdue()
    {
        await _service.UpdateOverdueInstallmentsAsync();
        return Ok(new { message = "تم تحديث حالة الأقساط المتأخرة" });
    }
}
