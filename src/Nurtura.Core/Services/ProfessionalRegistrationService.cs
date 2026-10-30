using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Services;

public class ProfessionalRegistrationService
{
    private readonly INurturaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfessionalRegistrationService(INurturaDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Creates the user, the Professional role link and the full profile (Pending) in one transaction:
    // if any step fails, nothing is saved.
    public async Task<RegistrationResult> RegisterAsync(
        ProfessionalRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var distinctTopics = registration.Topics.Distinct().ToList();
        if (distinctTopics.Count == 0)
        {
            throw new ArgumentException("At least one topic is required.", nameof(registration));
        }

        var distinctLanguages = registration.Languages.Distinct().ToList();
        if (distinctLanguages.Count == 0)
        {
            throw new ArgumentException("At least one language is required.", nameof(registration));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser { UserName = registration.Email, Email = registration.Email };

        var result = await _userManager.CreateAsync(user, registration.Password);
        if (!result.Succeeded)
        {
            return RegistrationResult.Failed(result.Errors);
        }

        result = await _userManager.AddToRoleAsync(user, Roles.Professional);
        if (!result.Succeeded)
        {
            return RegistrationResult.Failed(result.Errors);
        }

        var profile = new ProfessionalProfile
        {
            UserId = user.Id,
            DisplayName = registration.DisplayName.Trim(),
            ProfessionalType = registration.ProfessionalType,
            Bio = registration.Bio.Trim(),
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.ProfessionalProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ProfessionalCategories.AddRange(distinctTopics.Select(topic => new ProfessionalCategory
        {
            ProfessionalProfileId = profile.Id,
            Category = topic
        }));

        _db.ProfessionalLanguages.AddRange(distinctLanguages.Select(language => new ProfessionalLanguage
        {
            ProfessionalProfileId = profile.Id,
            Language = language
        }));

        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return RegistrationResult.Success(user);
    }
}
