namespace Nurtura.Core;

public interface INurturaDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
