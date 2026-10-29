using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nurtura.Core.Entities;

namespace Nurtura.Core;

public interface INurturaDbContext
{
    DbSet<MotherProfile> MotherProfiles { get; }

    DbSet<MotherConcern> MotherConcerns { get; }

    DbSet<MotherSupportType> MotherSupportTypes { get; }

    DbSet<Baby> Babies { get; }

    // Used for transactions that span Identity and Nurtura tables.
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
