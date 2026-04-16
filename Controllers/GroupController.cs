using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires login
public class GroupController : ControllerBase
{
    private readonly AppDbContext _context;

    public GroupController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var groups = await _context.Groups
            .Include(g => g.GroupPermissions).ThenInclude(gp => gp.Permission)
            .Select(g => new {
                g.Id, g.Name, g.Description,
                Permissions = g.GroupPermissions.Select(gp => gp.Permission.Name)
            }).ToListAsync();
        return Ok(groups);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGroupDto dto)
    {
        var group = new Group { Name = dto.Name, Description = dto.Description };
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        return Ok(group);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, CreateGroupDto dto)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group == null) return NotFound();

        group.Name = dto.Name;
        group.Description = dto.Description;

        await _context.SaveChangesAsync();
        return Ok(group);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group == null) return NotFound();
        if (group.Name == "Admin") return BadRequest(new { message = "لا يمكن حذف مجموعة المديرين" });

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف المجموعة بنجاح" });
    }
    

    [HttpPost("assign-permissions")]
    public async Task<IActionResult> AssignPermissions(AssignPermissionsDto dto)
    {
        var group = await _context.Groups.Include(g => g.GroupPermissions).FirstOrDefaultAsync(g => g.Id == dto.GroupId);
        if (group == null) return NotFound();

        _context.GroupPermissions.RemoveRange(group.GroupPermissions);
        
        foreach (var pid in dto.PermissionIds)
        {
            _context.GroupPermissions.Add(new GroupPermission { GroupId = dto.GroupId, PermissionId = pid });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعيين الصلاحيات بنجاح" });
    }
}
