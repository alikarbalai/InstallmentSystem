using System.ComponentModel.DataAnnotations;

namespace InstallmentSystem.Models.Identity;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }

    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    public ICollection<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>();
}

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty; // e.g. "Customer.Create"
    
    public string? Description { get; set; }

    public ICollection<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>();
}
