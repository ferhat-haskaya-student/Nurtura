using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Data;

public class NurturaDbContext : IdentityDbContext<ApplicationUser>, INurturaDbContext
{
    public NurturaDbContext(DbContextOptions<NurturaDbContext> options)
        : base(options)
    {
    }

    public DbSet<MotherProfile> MotherProfiles => Set<MotherProfile>();

    public DbSet<MotherConcern> MotherConcerns => Set<MotherConcern>();

    public DbSet<MotherSupportType> MotherSupportTypes => Set<MotherSupportType>();

    public DbSet<Baby> Babies => Set<Baby>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NurturaDbContext).Assembly);
    }
}
