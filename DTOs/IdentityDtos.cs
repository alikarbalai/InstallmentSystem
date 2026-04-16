namespace InstallmentSystem.DTOs;

public class CreateGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AssignPermissionsDto
{
    public Guid GroupId { get; set; }
    public List<Guid> PermissionIds { get; set; } = new();
}

public class AssignUserToGroupDto
{
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class CreatePermissionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
