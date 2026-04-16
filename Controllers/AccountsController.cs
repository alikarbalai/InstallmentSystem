using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using InstallmentSystem.Data;
using InstallmentSystem.Models;
using InstallmentSystem.DTOs;
using InstallmentSystem.Authorization;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AccountsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [HasPermission("Account.Read")]
    public async Task<IActionResult> GetAll()
    {
        var accounts = await _db.Accounts
            .Select(a => new AccountDto
            {
                Id       = a.Id,
                Code     = a.Code,
                Name     = a.Name,
                Type     = a.Type,
                ParentId = a.ParentId
            })
            .OrderBy(a => a.Code)
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpGet("tree")]
    [HasPermission("Account.Read")]
    public async Task<IActionResult> GetTree()
    {
        var accounts = await _db.Accounts.Include(a => a.JournalEntryDetails).ToListAsync();
        
        var dtoList = accounts.Select(a => {
            decimal totalDebit = a.JournalEntryDetails.Sum(d => d.Debit);
            decimal totalCredit = a.JournalEntryDetails.Sum(d => d.Credit);
            decimal balance = 0;
            
            if (a.Type == "Asset" || a.Type == "Expense")
                balance = totalDebit - totalCredit;
            else
                balance = totalCredit - totalDebit;

            return new AccountTreeDto
            {
                Id = a.Id, Code = a.Code, Name = a.Name, Type = a.Type, ParentId = a.ParentId, Balance = balance
            };
        }).ToList();

        var lookup = dtoList.ToDictionary(a => a.Id);
        var roots = new List<AccountTreeDto>();

        foreach (var item in dtoList)
        {
            if (item.ParentId.HasValue && lookup.ContainsKey(item.ParentId.Value))
            {
                lookup[item.ParentId.Value].Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        void RollupBalance(AccountTreeDto node)
        {
            foreach (var child in node.Children)
            {
                RollupBalance(child);
                node.Balance += child.Balance;
            }
        }

        // Sort children recursively
        void SortChildren(List<AccountTreeDto> nodes)
        {
            nodes.Sort((x, y) => string.Compare(x.Code, y.Code, StringComparison.Ordinal));
            foreach (var node in nodes)
            {
                SortChildren(node.Children);
            }
        }
        
        foreach (var root in roots)
        {
            RollupBalance(root);
        }

        SortChildren(roots);

        return Ok(roots);
    }

    [HttpPost]
    [HasPermission("Account.Create")]
    public async Task<IActionResult> Create([FromBody] CreateAccountDto dto)
    {
        if (await _db.Accounts.AnyAsync(a => a.Code == dto.Code))
            return BadRequest(new { message = "رمز الحساب مستخدم بالفعل" });

        if (dto.ParentId.HasValue)
        {
            var parent = await _db.Accounts.FindAsync(dto.ParentId.Value);
            if (parent == null)
                return BadRequest(new { message = "الحساب الأب غير موجود" });
        }

        var account = new Account
        {
            Code     = dto.Code,
            Name     = dto.Name,
            Type     = dto.Type,
            ParentId = dto.ParentId
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return Ok(new { message = "تم إنشاء الحساب بنجاح", id = account.Id });
    }

    [HttpPut("{id}")]
    [HasPermission("Account.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountDto dto)
    {
        var account = await _db.Accounts.FindAsync(id);
        if (account == null) return NotFound();

        if (await _db.Accounts.AnyAsync(a => a.Code == dto.Code && a.Id != id))
            return BadRequest(new { message = "رمز الحساب مستخدم بالفعل لحساب آخر" });

        if (dto.ParentId == id)
            return BadRequest(new { message = "لا يمكن أن يكون الحساب أباً لنفسه" });

        // Basic circular dependency check: cannot set parent to one of its children
        // For safety, not fully checking deep cyclic paths, but usually users don't make loops in small systems
        // A complete check would require walking up the tree. Let's do a simple one:
        var parentId = dto.ParentId;
        while (parentId.HasValue)
        {
            if (parentId == id) return BadRequest(new { message = "لا يمكن أن يكون الحساب أباً وحفيداً في نفس الوقت (حلقة دائرية)" });
            var p = await _db.Accounts.FindAsync(parentId.Value);
            parentId = p?.ParentId;
        }

        account.Code     = dto.Code;
        account.Name     = dto.Name;
        account.Type     = dto.Type;
        account.ParentId = dto.ParentId;

        await _db.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الحساب" });
    }

    [HttpDelete("{id}")]
    [HasPermission("Account.Delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var account = await _db.Accounts
            .Include(a => a.Children)
            .Include(a => a.JournalEntryDetails)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null) return NotFound();

        if (account.Children.Any())
            return BadRequest(new { message = "لا يمكن حذف حساب يحتوي على حسابات متفرعة" });

        if (account.JournalEntryDetails.Any())
            return BadRequest(new { message = "لا يمكن حذف حساب به قيود محاسبية مسجلة" });

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الحساب بنجاح" });
    }
}
