using Microsoft.AspNetCore.Identity;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Services;

public sealed record RegistrationResult(ApplicationUser? User, IReadOnlyList<IdentityError> Errors)
{
    public bool Succeeded => User is not null;

    public static RegistrationResult Success(ApplicationUser user) => new(user, []);

    public static RegistrationResult Failed(IEnumerable<IdentityError> errors) => new(null, errors.ToList());
}
