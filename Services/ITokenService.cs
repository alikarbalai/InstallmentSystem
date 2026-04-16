using InstallmentSystem.Models.Identity;

namespace InstallmentSystem.Services;

public interface ITokenService
{
    string CreateToken(ApplicationUser user, IEnumerable<string> permissions);
}
