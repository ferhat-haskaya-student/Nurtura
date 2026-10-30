using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Services;

public class MotherRegistrationService
{
    private readonly INurturaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MotherRegistrationService(INurturaDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Creates the user, the Mother role link and the profile in one transaction:
    // if any step fails, nothing is saved.
    public async Task<RegistrationResult> RegisterAsync(
        string displayName,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser { UserName = email, Email = email };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return RegistrationResult.Failed(result.Errors);
        }

        result = await _userManager.AddToRoleAsync(user, Roles.Mother);
        if (!result.Succeeded)
        {
            return RegistrationResult.Failed(result.Errors);
        }

        _db.MotherProfiles.Add(new MotherProfile
        {
            UserId = user.Id,
            DisplayName = displayName.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return RegistrationResult.Success(user);
    }
}
