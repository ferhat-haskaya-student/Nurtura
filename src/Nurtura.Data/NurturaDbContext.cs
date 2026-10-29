using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Core.Identity;

namespace Nurtura.Data;

public class NurturaDbContext : IdentityDbContext<ApplicationUser>, INurturaDbContext
{
    public NurturaDbContext(DbContextOptions<NurturaDbContext> options)
        : base(options)
    {
    }
}
