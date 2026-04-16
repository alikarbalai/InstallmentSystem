using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models.Identity;
using InstallmentSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstallmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _context;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        AppDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _userManager.FindByNameAsync(dto.Username) != null)
            return BadRequest(new { message = "اسم المستخدم موجود مسبقاً" });

        var user = new ApplicationUser
        {
            UserName = dto.Username,
            Email    = dto.Email,
            FullName = dto.FullName
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new { message = "تم إنشاء الحساب بنجاح" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _userManager.FindByNameAsync(dto.Username);
        if (user == null) return Unauthorized(new { message = "خطأ في اسم المستخدم أو كلمة المرور" });

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!result.Succeeded) return Unauthorized(new { message = "خطأ في اسم المستخدم أو كلمة المرور" });

        // Get permissions from groups
        var permissions = await _context.UserGroups
            .Where(ug => ug.UserId == user.Id)
            .SelectMany(ug => ug.Group.GroupPermissions)
            .Select(gp => gp.Permission.Name)
            .Distinct()
            .ToListAsync();

        user.LastLogin = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = _tokenService.CreateToken(user, permissions);

        return Ok(new AuthResponseDto
        {
            UserId      = user.Id.ToString(),
            Username    = user.UserName!,
            FullName    = user.FullName,
            Token       = token,
            Permissions = permissions
        });
    }
}
