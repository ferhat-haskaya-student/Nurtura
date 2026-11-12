using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The admin's Reports page: the queue of reported forum posts and comments, Dismiss / Hide / Remove, and the
// "Hidden by moderator" list. For the Admin role only; the caller checks the role. Reporters are never shown:
// no query here selects a reporter's id or name.
public class ForumModerationService
{
    private readonly INurturaDbContext _db;

    public ForumModerationService(INurturaDbContext db)
    {
        _db = db;
    }

    // How many posts and comments have at least one open report (the menu badge).
    public async Task<int> CountOpenItemsAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _db.PostReports.Where(r => r.Status == ReportStatus.Open).Select(r => r.ForumPostId).Distinct().CountAsync(cancellationToken);
        var comments = await _db.CommentReports.Where(r => r.Status == ReportStatus.Open).Select(r => r.ForumCommentId).Distinct().CountAsync(cancellationToken);
        return posts + comments;
    }

    // One card per reported item with open reports: "Someone may be in danger" first, then the oldest open report.
    public async Task<List<ReportCard>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var postReports = await _db.PostReports
            .Where(r => r.Status == ReportStatus.Open)
            .Select(r => new OpenReport(r.ForumPostId, r.Reason, r.Note, r.CreatedAt))
            .ToListAsync(cancellationToken);
        var commentReports = await _db.CommentReports
            .Where(r => r.Status == ReportStatus.Open)
            .Select(r => new OpenReport(r.ForumCommentId, r.Reason, r.Note, r.CreatedAt))
            .ToListAsync(cancellationToken);

        var postIds = postReports.Select(r => r.ItemId).Distinct().ToList();
        var posts = await _db.ForumPosts
            .Where(p => postIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id, p.Title, p.Body, p.CreatedAt, p.DeletedByAuthorAt, p.HiddenByModeratorAt, p.RemovedByModeratorAt,
                AuthorName = _db.MotherProfiles.Where(m => m.Id == p.AuthorProfileId).Select(m => m.DisplayName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var commentIds = commentReports.Select(r => r.ItemId).Distinct().ToList();
        var comments = await (
                from c in _db.ForumComments
                where commentIds.Contains(c.Id)
                join p in _db.ForumPosts on c.ForumPostId equals p.Id
                select new
                {
                    c.Id, c.ForumPostId, PostTitle = p.Title, c.Body, c.CreatedAt, c.HiddenByModeratorAt,
                    AuthorName = _db.MotherProfiles.Where(m => m.Id == c.AuthorProfileId).Select(m => m.DisplayName).FirstOrDefault()
                })
            .ToListAsync(cancellationToken);

        var cards = new List<ReportCard>();
        foreach (var post in posts)
        {
            var state = ForumPostStates.Of(post.DeletedByAuthorAt, post.HiddenByModeratorAt, post.RemovedByModeratorAt);
            cards.Add(Card(
                ModerationKind.Post, post.Id, post.Id, post.Title, post.Body, post.AuthorName, post.CreatedAt, state,
                postReports.Where(r => r.ItemId == post.Id).ToList()));
        }

        foreach (var comment in comments)
        {
            var state = comment.HiddenByModeratorAt is null ? ForumPostState.Visible : ForumPostState.HiddenByModerator;
            cards.Add(Card(
                ModerationKind.Comment, comment.Id, comment.ForumPostId, comment.PostTitle, comment.Body, comment.AuthorName,
                comment.CreatedAt, state, commentReports.Where(r => r.ItemId == comment.Id).ToList()));
        }

        return cards
            .OrderByDescending(c => c.Danger)
            .ThenBy(c => c.FirstReportedAt)
            .ToList();
    }

    // All open reports on the item are closed; the item is unchanged. False when it has no open reports.
    public async Task<bool> DismissAsync(ModerationKind kind, int id, CancellationToken cancellationToken = default) =>
        await CloseReportsAsync(kind, id, ReportStatus.Dismissed, cancellationToken) > 0;

    // Everyone sees "hidden by a moderator" in its place; nothing is cleared. Its open reports are closed and the
    // author gets one notification. False when it does not exist or cannot be hidden (already hidden, or a post
    // that was deleted or removed).
    public async Task<bool> HideAsync(ModerationKind kind, int id, CancellationToken cancellationToken = default)
    {
        if (await TargetAsync(kind, id, cancellationToken) is not { } target
            || target.State != ForumPostState.Visible)
        {
            return false;
        }

        var utcNow = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var changed = kind == ModerationKind.Post
            ? await _db.ForumPosts
                .Where(p => p.Id == id && p.HiddenByModeratorAt == null && p.DeletedByAuthorAt == null && p.RemovedByModeratorAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.HiddenByModeratorAt, utcNow), cancellationToken)
            : await _db.ForumComments
                .Where(c => c.Id == id && c.HiddenByModeratorAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.HiddenByModeratorAt, utcNow), cancellationToken);
        if (changed != 1)
        {
            return false;
        }

        await CloseReportsAsync(kind, id, ReportStatus.Actioned, cancellationToken);
        if (target.AuthorUserId is not null)
        {
            NotificationWriter.AddModeration(
                _db, target.AuthorUserId, kind == ModerationKind.Post ? NotificationType.PostHidden : NotificationType.CommentHidden,
                target.PostId, target.PostTitle);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Deleted for good, with the author-delete rules: a post without comments is deleted; a post with comments is
    // cleared and closed ("removed by a moderator") and its comments stay; a comment is deleted, and if it was the
    // last comment of a cleared post, that post goes too. The author gets one notification (without a link: the
    // post may be gone). False when it does not exist or was already deleted or removed.
    public async Task<bool> RemoveAsync(ModerationKind kind, int id, CancellationToken cancellationToken = default)
    {
        if (await TargetAsync(kind, id, cancellationToken) is not { } target
            || target.State is ForumPostState.DeletedByAuthor or ForumPostState.RemovedByModerator)
        {
            return false;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (kind == ModerationKind.Post)
        {
            var deleted = await _db.ForumPosts
                .Where(p => p.Id == id && !_db.ForumComments.Any(c => c.ForumPostId == p.Id))
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0)
            {
                var cleared = await _db.ForumPosts
                    .Where(p => p.Id == id && p.DeletedByAuthorAt == null && p.RemovedByModeratorAt == null)
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(p => p.Title, string.Empty)
                            .SetProperty(p => p.Body, string.Empty)
                            .SetProperty(p => p.RemovedByModeratorAt, DateTime.UtcNow),
                        cancellationToken);
                if (cleared != 1)
                {
                    return false;
                }

                await CloseReportsAsync(kind, id, ReportStatus.Actioned, cancellationToken);
            }
        }
        else
        {
            if (await _db.ForumComments.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken) != 1)
            {
                return false;
            }

            await _db.ForumPosts
                .Where(p => p.Id == target.PostId && (p.DeletedByAuthorAt != null || p.RemovedByModeratorAt != null)
                    && !_db.ForumComments.Any(c => c.ForumPostId == p.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (target.AuthorUserId is not null)
        {
            NotificationWriter.AddModeration(
                _db, target.AuthorUserId, kind == ModerationKind.Post ? NotificationType.PostRemoved : NotificationType.CommentRemoved,
                null, target.PostTitle);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Posts and comments a moderator hid (and that were not deleted or removed since), most recently hidden first.
    public async Task<List<HiddenItem>> GetHiddenAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _db.ForumPosts
            .Where(p => p.HiddenByModeratorAt != null && p.DeletedByAuthorAt == null && p.RemovedByModeratorAt == null)
            .Select(p => new HiddenItem(
                ModerationKind.Post, p.Id, p.Id, p.Title, p.Body,
                _db.MotherProfiles.Where(m => m.Id == p.AuthorProfileId).Select(m => m.DisplayName).FirstOrDefault(),
                p.HiddenByModeratorAt!.Value))
            .ToListAsync(cancellationToken);
        var comments = await (
                from c in _db.ForumComments
                where c.HiddenByModeratorAt != null
                join p in _db.ForumPosts on c.ForumPostId equals p.Id
                select new HiddenItem(
                    ModerationKind.Comment, c.Id, p.Id, p.Title, c.Body,
                    _db.MotherProfiles.Where(m => m.Id == c.AuthorProfileId).Select(m => m.DisplayName).FirstOrDefault(),
                    c.HiddenByModeratorAt!.Value))
            .ToListAsync(cancellationToken);
        return posts.Concat(comments).OrderByDescending(h => h.HiddenAt).ToList();
    }

    // Shown again to everyone. Nobody is notified. False when it was not hidden.
    public async Task<bool> UnhideAsync(ModerationKind kind, int id, CancellationToken cancellationToken = default)
    {
        var changed = kind == ModerationKind.Post
            ? await _db.ForumPosts
                .Where(p => p.Id == id && p.HiddenByModeratorAt != null)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.HiddenByModeratorAt, (DateTime?)null), cancellationToken)
            : await _db.ForumComments
                .Where(c => c.Id == id && c.HiddenByModeratorAt != null)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.HiddenByModeratorAt, (DateTime?)null), cancellationToken);
        return changed == 1;
    }

    private static ReportCard Card(
        ModerationKind kind,
        int id,
        int postId,
        string postTitle,
        string text,
        string? authorName,
        DateTime createdAt,
        ForumPostState state,
        List<OpenReport> reports)
    {
        return new ReportCard(
            kind,
            id,
            postId,
            postTitle,
            text,
            authorName,
            createdAt,
            state,
            reports.GroupBy(r => r.Reason)
                .OrderBy(g => g.Key)
                .Select(g => new ReasonCount(g.Key, g.Count()))
                .ToList(),
            reports.Where(r => !string.IsNullOrWhiteSpace(r.Note)).OrderBy(r => r.CreatedAt).Select(r => r.Note!).ToList(),
            reports.Count,
            reports.Any(r => r.Reason == ReportReason.SomeoneInDanger),
            reports.Min(r => r.CreatedAt));
    }

    private async Task<int> CloseReportsAsync(ModerationKind kind, int id, ReportStatus status, CancellationToken cancellationToken) =>
        kind == ModerationKind.Post
            ? await _db.PostReports
                .Where(r => r.ForumPostId == id && r.Status == ReportStatus.Open)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, status), cancellationToken)
            : await _db.CommentReports
                .Where(r => r.ForumCommentId == id && r.Status == ReportStatus.Open)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, status), cancellationToken);

    // The item, its post, the post's title (before any change) and the author's user id (null after account
    // deletion). For a comment, State is Visible or HiddenByModerator.
    private async Task<Target?> TargetAsync(ModerationKind kind, int id, CancellationToken cancellationToken)
    {
        if (kind == ModerationKind.Post)
        {
            var post = await _db.ForumPosts
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id, p.Title, p.DeletedByAuthorAt, p.HiddenByModeratorAt, p.RemovedByModeratorAt,
                    AuthorUserId = _db.MotherProfiles.Where(m => m.Id == p.AuthorProfileId).Select(m => m.UserId).FirstOrDefault()
                })
                .SingleOrDefaultAsync(cancellationToken);
            return post is null
                ? null
                : new Target(post.Id, post.Title, post.AuthorUserId,
                    ForumPostStates.Of(post.DeletedByAuthorAt, post.HiddenByModeratorAt, post.RemovedByModeratorAt));
        }

        var comment = await (
                from c in _db.ForumComments
                where c.Id == id
                join p in _db.ForumPosts on c.ForumPostId equals p.Id
                select new
                {
                    PostId = p.Id, p.Title, c.HiddenByModeratorAt,
                    AuthorUserId = _db.MotherProfiles.Where(m => m.Id == c.AuthorProfileId).Select(m => m.UserId).FirstOrDefault()
                })
            .SingleOrDefaultAsync(cancellationToken);
        return comment is null
            ? null
            : new Target(comment.PostId, comment.Title, comment.AuthorUserId,
                comment.HiddenByModeratorAt is null ? ForumPostState.Visible : ForumPostState.HiddenByModerator);
    }

    private sealed record OpenReport(int ItemId, ReportReason Reason, string? Note, DateTime CreatedAt);

    private sealed record Target(int PostId, string PostTitle, string? AuthorUserId, ForumPostState State);
}

public enum ModerationKind
{
    Post,
    Comment
}

public sealed record ReasonCount(ReportReason Reason, int Count);

// One reported post or comment in the queue. Id is the item; PostId and PostTitle are its post (for a post, itself).
// State: for a post, what mothers see; for a comment, Visible or HiddenByModerator. Times are UTC.
public sealed record ReportCard(
    ModerationKind Kind,
    int Id,
    int PostId,
    string PostTitle,
    string Text,
    string? AuthorName,
    DateTime CreatedAt,
    ForumPostState State,
    List<ReasonCount> Reasons,
    List<string> Notes,
    int ReportCount,
    bool Danger,
    DateTime FirstReportedAt)
{
    // A post deleted by its author (or already removed) can only be dismissed.
    public bool CanHide => State == ForumPostState.Visible;

    public bool CanRemove => State is ForumPostState.Visible or ForumPostState.HiddenByModerator;
}

// One item on the "Hidden by moderator" list. Times are UTC.
public sealed record HiddenItem(
    ModerationKind Kind,
    int Id,
    int PostId,
    string PostTitle,
    string Text,
    string? AuthorName,
    DateTime HiddenAt);
