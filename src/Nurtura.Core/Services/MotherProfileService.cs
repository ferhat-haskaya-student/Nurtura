using Microsoft.EntityFrameworkCore;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id and works only on that user's own profile.
public class MotherProfileService
{
    private readonly INurturaDbContext _db;

    public MotherProfileService(INurturaDbContext db)
    {
        _db = db;
    }

    // Null when the user has no mother profile.
    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
