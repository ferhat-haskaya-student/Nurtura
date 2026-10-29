using System.Security.Claims;
using Nurtura.Core.Identity;

namespace Nurtura.Web.Identity;

// Each role has an MVC area with the same name. These return the area of the user's home page, or null.
public static class RoleHome
{
    private static readonly string[] Priority = [Roles.Admin, Roles.Professional, Roles.Mother];

    public static string? AreaFor(ClaimsPrincipal user) =>
        Priority.FirstOrDefault(user.IsInRole);

    public static string? AreaFor(IEnumerable<string> roles) =>
        Priority.FirstOrDefault(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase));
}
