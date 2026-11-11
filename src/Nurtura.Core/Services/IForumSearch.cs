namespace Nurtura.Core.Services;

// Forum search over post titles and bodies. The PostgreSQL full-text implementation lives in Nurtura.Data.
public interface IForumSearch
{
    // Ids of matching posts, best match first, at most max. Posts deleted by their author and posts this mother
    // hid are left out. The search text must never be stored or logged.
    Task<IReadOnlyList<int>> SearchAsync(
        string search,
        int motherProfileId,
        int max,
        CancellationToken cancellationToken = default);
}
