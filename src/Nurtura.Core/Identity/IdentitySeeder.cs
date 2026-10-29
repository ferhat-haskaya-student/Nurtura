using Microsoft.AspNetCore.Identity;

namespace Nurtura.Core.Identity;

public static class IdentitySeeder
{
    public const string TestMotherEmail = "mother@nurtura.com";
    public const string TestProfessionalEmail = "professional@nurtura.com";

    public static async Task SeedAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        string adminEmail,
        string adminPassword)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)));
            }
        }

        await EnsureUserAsync(userManager, adminEmail, adminPassword, Roles.Admin);
    }

    // Test accounts for local development. The caller decides when this runs.
    public static async Task SeedTestUsersAsync(UserManager<ApplicationUser> userManager, string password)
    {
        await EnsureUserAsync(userManager, TestMotherEmail, password, Roles.Mother);
        await EnsureUserAsync(userManager, TestProfessionalEmail, password, Roles.Professional);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
