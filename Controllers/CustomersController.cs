using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models;
using InstallmentSystem.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db) => _db = db;

    [HttpGet]
    [HasPermission("Customer.Read")]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _db.Customers
            .Select(c => new {
                c.Id, c.FullName, c.Phone, c.Address, c.NationalId, c.CreatedAt,
                ContractsCount = c.Contracts.Count,
                TotalDebt = c.Contracts.Sum(x => x.RemainingAmount)
            }).ToListAsync();
        return Ok(customers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var customer = await _db.Customers
            .Include(c => c.Contracts)
                .ThenInclude(c => c.Installments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null) return NotFound();
        return Ok(customer);
    }

    [HttpPost]
    [HasPermission("Customer.Create")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        var customer = new Customer
        {
            FullName = dto.FullName,
            Phone = dto.Phone,
            Address = dto.Address,
            NationalId = dto.NationalId
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id}")]
    [HasPermission("Customer.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerDto dto)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        customer.FullName = dto.FullName;
        customer.Phone = dto.Phone;
        customer.Address = dto.Address;
        customer.NationalId = dto.NationalId;

        await _db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpDelete("{id}")]
    [HasPermission("Customer.Delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/statement")]
    public async Task<IActionResult> GetStatement(Guid id)
    {
        var customer = await _db.Customers
            .Include(c => c.Contracts)
                .ThenInclude(c => c.Installments)
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null) return NotFound();

        var statement = new
        {
            customer.FullName,
            customer.Phone,
            TotalContracts = customer.Contracts.Count,
            TotalAmount = customer.Contracts.Sum(c => c.TotalAmount),
            TotalPaid = customer.Payments.Sum(p => p.Amount),
            TotalRemaining = customer.Contracts.Sum(c => c.RemainingAmount),
            Contracts = customer.Contracts.Select(c => new {
                c.ContractNumber, c.TotalAmount, c.RemainingAmount, c.Status,
                PaidInstallments = c.Installments.Count(i => i.Status == InstallmentStatus.Paid),
                TotalInstallments = c.Installments.Count,
                OverdueInstallments = c.Installments.Count(i => i.Status == InstallmentStatus.Overdue)
            })
        };

        return Ok(statement);
    }
}
