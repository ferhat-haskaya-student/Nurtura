using Microsoft.AspNetCore.Identity;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Services;

public sealed record MotherRegistrationResult(ApplicationUser? User, IReadOnlyList<IdentityError> Errors)
{
    public bool Succeeded => User is not null;

    public static MotherRegistrationResult Success(ApplicationUser user) => new(user, []);

    public static MotherRegistrationResult Failed(IEnumerable<IdentityError> errors) => new(null, errors.ToList());
}
