using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Core.Identity;

public static class IdentitySeeder
{
    public const string TestMotherEmail = "mother@nurtura.com";
    public const string TestProfessionalEmail = "professional@nurtura.com";
    public const string TestMotherDisplayName = "Test Mother";
    public const string TestBabyName = "Test Baby";

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
    public static async Task SeedTestUsersAsync(
        UserManager<ApplicationUser> userManager,
        INurturaDbContext db,
        string password)
    {
        var mother = await EnsureUserAsync(userManager, TestMotherEmail, password, Roles.Mother);
        await EnsureUserAsync(userManager, TestProfessionalEmail, password, Roles.Professional);

        if (!await db.MotherProfiles.AnyAsync(p => p.UserId == mother.Id))
        {
            db.MotherProfiles.Add(new MotherProfile
            {
                UserId = mother.Id,
                DisplayName = TestMotherDisplayName,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // The test mother skips onboarding. Does nothing if she is already onboarded.
        await new MotherOnboardingService(db).CompleteAsync(
            mother.Id,
            new OnboardingAnswers(
                Concerns: [Concern.BabySleep],
                SupportTypes: [SupportType.TalkingToProfessional],
                IsFirstBaby: true,
                HelpAtHome: HelpAtHome.Sometimes,
                BabyName: TestBabyName,
                BabyBirthDate: DateOnly.FromDateTime(DateTime.Today).AddMonths(-3),
                BirthType: BirthType.Natural,
                BirthWeek: Baby.BirthWeekDefault,
                FeedingType: BabyFeedingType.Breast));
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        return user;
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
