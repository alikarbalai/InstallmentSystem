namespace InstallmentSystem.Models.Identity;

public class UserGroup
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
}

public class GroupPermission
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
