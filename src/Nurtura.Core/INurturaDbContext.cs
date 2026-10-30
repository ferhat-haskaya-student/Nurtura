using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core;

public interface INurturaDbContext
{
    DbSet<MotherProfile> MotherProfiles { get; }

    DbSet<MotherConcern> MotherConcerns { get; }

    DbSet<MotherSupportType> MotherSupportTypes { get; }

    DbSet<Baby> Babies { get; }

    DbSet<ProfessionalProfile> ProfessionalProfiles { get; }

    DbSet<ProfessionalCategory> ProfessionalCategories { get; }

    DbSet<ProfessionalLanguage> ProfessionalLanguages { get; }

    // Identity's user table; implemented by IdentityDbContext.
    DbSet<ApplicationUser> Users { get; }

    // Used for transactions that span Identity and Nurtura tables.
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
