using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public UserController(UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userManager.Users
            .Select(u => new { 
                u.Id, u.UserName, u.FullName, u.Email,
                GroupId = _context.UserGroups.Where(ug => ug.UserId == u.Id).Select(ug => ug.GroupId).FirstOrDefault(),
                GroupName = _context.UserGroups.Where(ug => ug.UserId == u.Id).Select(ug => ug.Group.Name).FirstOrDefault()
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RegisterDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        user.FullName = dto.FullName;
        user.Email = dto.Email;
        user.UserName = dto.Username;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();
        if (user.UserName == "admin") return BadRequest(new { message = "لا يمكن حذف المستخدم الرئيسي" });

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "تم حذف المستخدم بنجاح" });
    }

    [HttpPost("assign-group")]
    public async Task<IActionResult> AssignToGroup(AssignUserToGroupDto dto)
    {
        // Enforce only ONE group per user - Remove existing associations
        var existingGroups = await _context.UserGroups.Where(ug => ug.UserId == dto.UserId).ToListAsync();
        _context.UserGroups.RemoveRange(existingGroups);

        _context.UserGroups.Add(new UserGroup { UserId = dto.UserId, GroupId = dto.GroupId });
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعيين المستخدم للمجموعة بنجاح" });
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(Guid id)
    {
        var permissions = await _context.UserGroups
            .Where(ug => ug.UserId == id)
            .SelectMany(ug => ug.Group.GroupPermissions)
            .Select(gp => gp.Permission.Name)
            .Distinct()
            .ToListAsync();
        return Ok(permissions);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }
}
