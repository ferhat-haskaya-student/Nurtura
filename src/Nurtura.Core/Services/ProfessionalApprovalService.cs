using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id and works only on that user's own profile.
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
}
