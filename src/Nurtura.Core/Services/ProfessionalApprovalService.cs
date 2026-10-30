using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// GetStatusAsync takes the logged-in user's id and reads only that user's own profile.
// The other methods are for the Admin role only; the caller checks the role.
public class ProfessionalApprovalService
{
    private readonly INurturaDbContext _db;

    public ProfessionalApprovalService(INurturaDbContext db)
    {
        _db = db;
    }

    // Null when the user has no professional profile.
    public async Task<ApprovalStatus?> GetStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.ProfessionalProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (ApprovalStatus?)p.ApprovalStatus)
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Professionals waiting for approval, oldest sign-up first.
    public async Task<IReadOnlyList<PendingProfessional>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ProfessionalProfiles
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new PendingProfessional(
                p.Id,
                p.DisplayName,
                u.Email,
                p.ProfessionalType,
                _db.ProfessionalCategories
                    .Where(c => c.ProfessionalProfileId == p.Id)
                    .OrderBy(c => c.Id)
                    .Select(c => c.Category)
                    .ToList(),
                _db.ProfessionalLanguages
                    .Where(l => l.ProfessionalProfileId == p.Id)
                    .OrderBy(l => l.Id)
                    .Select(l => l.Language)
                    .ToList(),
                p.Bio,
                p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    // Returns the display name, or null when there is no such profile or it is no longer Pending.
    public Task<string?> ApproveAsync(int profileId, CancellationToken cancellationToken = default) =>
        SetStatusAsync(profileId, ApprovalStatus.Approved, cancellationToken);

    // Returns the display name, or null when there is no such profile or it is no longer Pending.
    public Task<string?> RejectAsync(int profileId, CancellationToken cancellationToken = default) =>
        SetStatusAsync(profileId, ApprovalStatus.Rejected, cancellationToken);

    // Only a Pending profile changes; anything else is left as it is.
    private async Task<string?> SetStatusAsync(
        int profileId,
        ApprovalStatus status,
        CancellationToken cancellationToken)
    {
        var profile = await _db.ProfessionalProfiles.SingleOrDefaultAsync(
            p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Pending,
            cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.ApprovalStatus = status;
        await _db.SaveChangesAsync(cancellationToken);
        return profile.DisplayName;
    }
}
