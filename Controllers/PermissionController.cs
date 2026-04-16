using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionController : ControllerBase
{
    private readonly AppDbContext _context;

    public PermissionController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var perms = await _context.Permissions.ToListAsync();
        return Ok(perms);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePermissionDto dto)
    {
        var perm = new Permission { Name = dto.Name, Description = dto.Description };
        _context.Permissions.Add(perm);
        await _context.SaveChangesAsync();
        return Ok(perm);
    }
}
