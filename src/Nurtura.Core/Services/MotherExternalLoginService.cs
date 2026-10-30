using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Services;

public enum MotherExternalLoginOutcome
{
    // The login is linked; the caller can sign her in.
    Ready,

    // The email belongs to a Professional or Admin; they must use email and password.
    NotMother,

    Failed
}

// Sign-in with an external provider (Google) is for mothers only.
public class MotherExternalLoginService
{
    private readonly INurturaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MotherExternalLoginService(INurturaDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Called when the external login is not linked to any account yet. The email must be verified by the provider.
    // An existing Mother account with this email gets the login linked. A new email gets a Mother account
    // without a password, plus her profile, in one transaction: if any step fails, nothing is saved.
    public async Task<MotherExternalLoginOutcome> LinkOrRegisterAsync(
        UserLoginInfo login,
        string email,
        string? name,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] != Roles.Mother)
            {
                return MotherExternalLoginOutcome.NotMother;
            }

            if (!(await _userManager.AddLoginAsync(user, login)).Succeeded)
            {
                return MotherExternalLoginOutcome.Failed;
            }

            await transaction.CommitAsync(cancellationToken);
            return MotherExternalLoginOutcome.Ready;
        }

        user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };

        if (!(await _userManager.CreateAsync(user)).Succeeded
            || !(await _userManager.AddToRoleAsync(user, Roles.Mother)).Succeeded
            || !(await _userManager.AddLoginAsync(user, login)).Succeeded)
        {
            return MotherExternalLoginOutcome.Failed;
        }

        _db.MotherProfiles.Add(new MotherProfile
        {
            UserId = user.Id,
            DisplayName = DisplayNameFrom(name, email),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return MotherExternalLoginOutcome.Ready;
    }

    // Her name from the provider, or the part of the email before "@" if there is none; cut to the maximum length.
    private static string DisplayNameFrom(string? name, string email)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name.Trim();
        return displayName.Length <= MotherProfile.DisplayNameMaxLength
            ? displayName
            : displayName[..MotherProfile.DisplayNameMaxLength].TrimEnd();
    }
}
