using InstallmentSystem.Models;
using InstallmentSystem.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InstallmentSystem.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        // 1. Ensure Permissions exist
        var entities = new[] { 
            "Customer", "Currency", "Contract", "Payment", 
            "Account", "Journal", "Receipt", "Product", "User", "Group" 
        };
        var actions = new[] { "Create", "Read", "Update", "Delete" };
        
        var allPermissionNames = new List<string>();
        foreach (var entity in entities)
            foreach (var action in actions)
                allPermissionNames.Add($"{entity}.{action}");

        var existingPermissions = await context.Permissions.ToListAsync();
        var newPermissions = allPermissionNames
            .Where(name => !existingPermissions.Any(p => p.Name == name))
            .Select(name => new Permission { Name = name })
            .ToList();

        if (newPermissions.Any())
        {
            context.Permissions.AddRange(newPermissions);
            await context.SaveChangesAsync();
        }
        // 2. Ensure Admin Group exists
        var adminGroup = await context.Groups.FirstOrDefaultAsync(g => g.Name == "Admin");
        if (adminGroup == null)
        {
            adminGroup = new Group { Name = "Admin", Description = "مدير النظام - كامل الصلاحيات" };
            context.Groups.Add(adminGroup);
            await context.SaveChangesAsync();
        }

        // 3. Assign all permissions to Admin Group
        var allPermissions = await context.Permissions.ToListAsync();
        var currentAdminPermissions = await context.GroupPermissions
            .Where(gp => gp.GroupId == adminGroup.Id)
            .ToListAsync();

        var missingAdminPermissions = allPermissions
            .Where(p => !currentAdminPermissions.Any(gp => gp.PermissionId == p.Id))
            .Select(p => new GroupPermission { GroupId = adminGroup.Id, PermissionId = p.Id });

        if (missingAdminPermissions.Any())
        {
            context.GroupPermissions.AddRange(missingAdminPermissions);
            await context.SaveChangesAsync();
        }

        // 4. Ensure Admin User exists
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@system.com",
                FullName = "Administrator",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                // Add to Admin Group
                context.UserGroups.Add(new UserGroup { UserId = adminUser.Id, GroupId = adminGroup.Id });
                await context.SaveChangesAsync();
            }
        }

        // 5. Ensure Default Accounts exist
        var defaultAccounts = new List<Account>
        {
            new Account { Id = new Guid("11111111-1111-1111-1111-111111111111"), Name = "النقدية", Code = "101", Type = "Asset" },
            new Account { Id = new Guid("22222222-2222-2222-2222-222222222222"), Name = "ذمم العملاء", Code = "102", Type = "Asset" },
            new Account { Id = new Guid("33333333-3333-3333-3333-333333333333"), Name = "الإيرادات", Code = "401", Type = "Revenue" },
            new Account { Id = new Guid("44444444-4444-4444-4444-444444444444"), Name = "الفوائد والأرباح", Code = "402", Type = "Revenue" },
            new Account { Id = new Guid("55555555-5555-5555-5555-555555555555"), Name = "البنك", Code = "103", Type = "Asset" }
        };

        foreach (var acc in defaultAccounts)
        {
            if (!await context.Accounts.AnyAsync(a => a.Id == acc.Id))
            {
                context.Accounts.Add(acc);
            }
        }
        await context.SaveChangesAsync();
    }
}
