using Microsoft.EntityFrameworkCore;
using Nurtura.Core;

namespace Nurtura.Data;

public class NurturaDbContext : DbContext, INurturaDbContext
{
    public NurturaDbContext(DbContextOptions<NurturaDbContext> options)
        : base(options)
    {
    }
}
