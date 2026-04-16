using Microsoft.AspNetCore.Authorization;

namespace InstallmentSystem.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) : base(permission)
    {
    }
}
