using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The mothers' forum. Every method takes the logged-in user's id and works through her own mother profile, found
// by that id; without one, nothing is shown or changed. Changing a post or comment is checked in the query itself:
// only rows whose author is her profile are updated or deleted, and editing also needs the row to be younger than
// 30 minutes by the server clock. Another mother's id matches nothing, so the caller answers 404.
// Text is plain: "\r\n" becomes "\n" and it is trimmed; it is never turned into HTML.
public class ForumService
{
    public const int PageSize = 20;
    public const int SearchMax = 20;
    public const int SearchMaxLength = 200;

    private readonly INurturaDbContext _db;
    private readonly IForumSearch _search;

    public ForumService(INurturaDbContext db, IForumSearch search)
    {
        _db = db;
        _search = search;
    }

    // The nine categories in their fixed order.
    public async Task<List<ForumCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await _db.ForumCategories.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);

    // Newest first, PageSize per page. Null when she has no mother profile.
    public async Task<ForumList?> GetListAsync(string userId, ForumFilter filter, CancellationToken cancellationToken = default)
    {
        if (await ProfileIdAsync(userId, cancellationToken) is not { } me)
        {
            return null;
        }

        var posts = _db.ForumPosts.AsQueryable();
        var hidden = _db.HiddenPosts.Where(h => h.MotherProfileId == me);
        if (filter.Hidden)
        {
            posts = posts.Where(p => hidden.Any(h => h.ForumPostId == p.Id));
        }
        else
        {
            posts = posts.Where(p => !hidden.Any(h => h.ForumPostId == p.Id));
            if (filter.Tab == ForumTab.Saved)
            {
                posts = posts.Where(p => _db.SavedPosts.Any(s => s.MotherProfileId == me && s.ForumPostId == p.Id));
            }
            else if (filter.Tab == ForumTab.Mine)
            {
                posts = posts.Where(p => p.AuthorProfileId == me && p.DeletedByAuthorAt == null);
            }
        }

        if (filter.CategoryId is { } categoryId)
        {
            posts = posts.Where(p => p.ForumCategoryId == categoryId);
        }

        if (filter.Language is { } language)
        {
            posts = posts.Where(p => p.Language == language);
        }

        var total = await posts.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (total + PageSize - 1) / PageSize);
        var page = Math.Clamp(filter.Page, 1, totalPages);

        var categories = await GetCategoriesAsync(cancellationToken);
        var cards = await CardsAsync(
            posts.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).Skip((page - 1) * PageSize).Take(PageSize),
            categories,
            cancellationToken);
        return new ForumList(categories, cards, page, totalPages);
    }

    // The best SearchMax matches over titles and bodies, without paging. Null when the search is blank or she has
    // no mother profile. The text is used only for this one query: never stored or logged.
    public async Task<List<ForumPostCard>?> SearchAsync(string userId, string? search, CancellationToken cancellationToken = default)
    {
        search = search?.Trim();
        if (string.IsNullOrEmpty(search) || await ProfileIdAsync(userId, cancellationToken) is not { } me)
        {
            return null;
        }

        if (search.Length > SearchMaxLength)
        {
            search = search[..SearchMaxLength];
        }

        var ids = (await _search.SearchAsync(search, me, SearchMax, cancellationToken)).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var cards = await CardsAsync(
            _db.ForumPosts.Where(p => ids.Contains(p.Id)),
            await GetCategoriesAsync(cancellationToken),
            cancellationToken);
        var rank = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return cards.OrderBy(c => rank[c.Id]).ToList();
    }

    // The post page, including a post she hid (opened directly) and one deleted by its author. Null when it does
    // not exist or she has no mother profile.
    public async Task<ForumPostView?> GetPostAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (await ProfileIdAsync(userId, cancellationToken) is not { } me)
        {
            return null;
        }

        var post = await _db.ForumPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (post is null)
        {
            return null;
        }

        var comments = await _db.ForumComments
            .AsNoTracking()
            .Where(c => c.ForumPostId == id)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                Comment = c,
                AuthorName = _db.MotherProfiles.Where(m => m.Id == c.AuthorProfileId).Select(m => m.DisplayName).FirstOrDefault(),
                HasReported = _db.CommentReports.Any(r => r.ForumCommentId == c.Id && r.ReporterProfileId == me)
            })
            .ToListAsync(cancellationToken);

        var authorName = await _db.MotherProfiles
            .Where(m => m.Id == post.AuthorProfileId)
            .Select(m => m.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        var categoryKey = await _db.ForumCategories
            .Where(c => c.Id == post.ForumCategoryId)
            .Select(c => c.Key)
            .SingleAsync(cancellationToken);
        var isSaved = await _db.SavedPosts.AnyAsync(s => s.MotherProfileId == me && s.ForumPostId == id, cancellationToken);
        var isHidden = await _db.HiddenPosts.AnyAsync(h => h.MotherProfileId == me && h.ForumPostId == id, cancellationToken);
        var hasReported = await _db.PostReports.AnyAsync(r => r.ReporterProfileId == me && r.ForumPostId == id, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var isDeleted = post.DeletedByAuthorAt is not null;
        var isMine = post.AuthorProfileId == me;
        return new ForumPostView(
            post.Id,
            post.Title,
            post.Body,
            categoryKey,
            post.Language,
            authorName,
            post.CreatedAt,
            post.EditedAt,
            isDeleted,
            isMine,
            isMine && !isDeleted && post.CreatedAt + ForumPost.EditWindow > utcNow,
            isSaved,
            isHidden,
            hasReported,
            comments.Select(c =>
            {
                var commentIsMine = c.Comment.AuthorProfileId == me;
                return new ForumCommentView(
                    c.Comment.Id,
                    c.AuthorName,
                    c.Comment.Body,
                    c.Comment.CreatedAt,
                    c.Comment.EditedAt,
                    commentIsMine,
                    commentIsMine && c.Comment.CreatedAt + ForumComment.EditWindow > utcNow,
                    c.HasReported);
            }).ToList());
    }

    // A new post. Urgent when her title or text contains one of the Assistant's urgent words; the post is
    // published either way.
    public async Task<ForumSaveResult> CreatePostAsync(string userId, ForumPostInput input, CancellationToken cancellationToken = default)
    {
        if (await ProfileIdAsync(userId, cancellationToken) is not { } me)
        {
            return ForumSaveResult.NotFound;
        }

        if (await CleanPostAsync(input, cancellationToken) is not { } clean)
        {
            return ForumSaveResult.Invalid;
        }

        var post = new ForumPost
        {
            AuthorProfileId = me,
            ForumCategoryId = input.CategoryId,
            Language = input.Language,
            Title = clean.Title,
            Body = clean.Body,
            CreatedAt = DateTime.UtcNow
        };
        _db.ForumPosts.Add(post);
        await _db.SaveChangesAsync(cancellationToken);

        return new ForumSaveResult(ForumSaveStatus.Saved, post.Id, AssistantText.IsUrgent(clean.Title + "\n" + clean.Body));
    }

    // Her own post's current values, only while she may still edit it. Null otherwise.
    public async Task<ForumPostEdit?> GetPostEditAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var editableSince = DateTime.UtcNow - ForumPost.EditWindow;
        return await OwnPosts(userId)
            .Where(p => p.Id == id && p.DeletedByAuthorAt == null && p.CreatedAt > editableSince)
            .Select(p => new ForumPostEdit(p.Id, p.ForumCategoryId, p.Language, p.Title, p.Body, p.CreatedAt + ForumPost.EditWindow))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // True when the post is hers and not deleted (whether or not the 30 minutes have passed).
    public async Task<bool> IsOwnPostAsync(string userId, int id, CancellationToken cancellationToken = default) =>
        await OwnPosts(userId).AnyAsync(p => p.Id == id && p.DeletedByAuthorAt == null, cancellationToken);

    // The post of her own comment; null when the comment is not hers.
    public async Task<int?> OwnCommentPostIdAsync(string userId, int id, CancellationToken cancellationToken = default) =>
        await OwnComments(userId).Where(c => c.Id == id).Select(c => (int?)c.ForumPostId).SingleOrDefaultAsync(cancellationToken);

    // Edits her own post within 30 minutes of posting. The conditional UPDATE repeats every check, so a form
    // opened in time but sent late changes nothing.
    public async Task<ForumSaveResult> EditPostAsync(string userId, int id, ForumPostInput input, CancellationToken cancellationToken = default)
    {
        if (!await OwnPosts(userId).AnyAsync(p => p.Id == id && p.DeletedByAuthorAt == null, cancellationToken))
        {
            return ForumSaveResult.NotFound;
        }

        if (await CleanPostAsync(input, cancellationToken) is not { } clean)
        {
            return ForumSaveResult.Invalid;
        }

        var utcNow = DateTime.UtcNow;
        var editableSince = utcNow - ForumPost.EditWindow;
        var changed = await OwnPosts(userId)
            .Where(p => p.Id == id && p.DeletedByAuthorAt == null && p.CreatedAt > editableSince)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.Title, clean.Title)
                    .SetProperty(p => p.Body, clean.Body)
                    .SetProperty(p => p.ForumCategoryId, input.CategoryId)
                    .SetProperty(p => p.Language, input.Language)
                    .SetProperty(p => p.EditedAt, utcNow),
                cancellationToken);
        return changed == 1 ? new ForumSaveResult(ForumSaveStatus.Saved, id) : ForumSaveResult.EditWindowPassed;
    }

    // Deletes her own post, at any time. With comments it keeps its place: title and body are cleared and it takes
    // no new comments. Without comments the row is removed. Either way her comment notifications for it go too, so
    // the old title does not stay in her list. False when it is not hers or already deleted.
    public async Task<bool> DeletePostAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (await ProfileIdAsync(userId, cancellationToken) is not { } me
            || !await _db.ForumPosts.AnyAsync(p => p.Id == id && p.AuthorProfileId == me && p.DeletedByAuthorAt == null, cancellationToken))
        {
            return false;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Notifications
            .Where(n => n.UserId == userId && n.Type == NotificationType.CommentOnYourPost && n.ForumPostId == id)
            .ExecuteDeleteAsync(cancellationToken);

        // Removed only if it still has no comments at this moment; otherwise cleared.
        var removed = await _db.ForumPosts
            .Where(p => p.Id == id && p.AuthorProfileId == me && !_db.ForumComments.Any(c => c.ForumPostId == p.Id))
            .ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            await _db.ForumPosts
                .Where(p => p.Id == id && p.AuthorProfileId == me && p.DeletedByAuthorAt == null)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(p => p.Title, string.Empty)
                        .SetProperty(p => p.Body, string.Empty)
                        .SetProperty(p => p.DeletedByAuthorAt, DateTime.UtcNow),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // A new comment on a post that is not deleted. The post's author gets one notification, unless she wrote the
    // comment herself or her account was deleted. Urgent as for posts.
    public async Task<ForumSaveResult> AddCommentAsync(string userId, int postId, string? body, CancellationToken cancellationToken = default)
    {
        var me = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        var post = await _db.ForumPosts
            .Where(p => p.Id == postId)
            .Select(p => new { p.Title, p.AuthorProfileId, p.DeletedByAuthorAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (me is null || post is null)
        {
            return ForumSaveResult.NotFound;
        }

        if (post.DeletedByAuthorAt is not null)
        {
            return ForumSaveResult.Closed;
        }

        var clean = CleanBody(body);
        if (clean.Length == 0 || clean.Length > ForumComment.BodyMaxLength)
        {
            return ForumSaveResult.Invalid;
        }

        _db.ForumComments.Add(new ForumComment
        {
            ForumPostId = postId,
            AuthorProfileId = me.Id,
            Body = clean,
            CreatedAt = DateTime.UtcNow
        });

        if (post.AuthorProfileId is { } authorId && authorId != me.Id)
        {
            var authorUserId = await _db.MotherProfiles
                .Where(p => p.Id == authorId)
                .Select(p => p.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (authorUserId is not null)
            {
                NotificationWriter.AddForumComment(_db, authorUserId, postId, me.DisplayName, post.Title);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ForumSaveResult(ForumSaveStatus.Saved, postId, AssistantText.IsUrgent(clean));
    }

    // Her own comment's current text, only while she may still edit it. Null otherwise.
    public async Task<ForumCommentEdit?> GetCommentEditAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var editableSince = DateTime.UtcNow - ForumComment.EditWindow;
        return await OwnComments(userId)
            .Where(c => c.Id == id && c.CreatedAt > editableSince)
            .Select(c => new ForumCommentEdit(c.Id, c.ForumPostId, c.Body, c.CreatedAt + ForumComment.EditWindow))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Edits her own comment within 30 minutes of writing it; the conditional UPDATE repeats every check.
    public async Task<ForumSaveResult> EditCommentAsync(string userId, int id, string? body, CancellationToken cancellationToken = default)
    {
        var postId = await OwnComments(userId)
            .Where(c => c.Id == id)
            .Select(c => (int?)c.ForumPostId)
            .SingleOrDefaultAsync(cancellationToken);
        if (postId is null)
        {
            return ForumSaveResult.NotFound;
        }

        var clean = CleanBody(body);
        if (clean.Length == 0 || clean.Length > ForumComment.BodyMaxLength)
        {
            return ForumSaveResult.Invalid;
        }

        var utcNow = DateTime.UtcNow;
        var editableSince = utcNow - ForumComment.EditWindow;
        var changed = await OwnComments(userId)
            .Where(c => c.Id == id && c.CreatedAt > editableSince)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.Body, clean)
                    .SetProperty(c => c.EditedAt, utcNow),
                cancellationToken);
        return changed == 1
            ? new ForumSaveResult(ForumSaveStatus.Saved, postId.Value)
            : ForumSaveResult.EditWindowPassed with { PostId = postId.Value };
    }

    // Deletes her own comment, at any time. If it was the last comment of a post deleted by its author, that post
    // is removed too ("without comments it disappears"). Found = false when it is not hers; PostId is null when the
    // post is gone.
    public async Task<(bool Found, int? PostId)> DeleteCommentAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var postId = await OwnComments(userId)
            .Where(c => c.Id == id)
            .Select(c => (int?)c.ForumPostId)
            .SingleOrDefaultAsync(cancellationToken);
        if (postId is null)
        {
            return (false, null);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await OwnComments(userId).Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken);
        var postRemoved = await _db.ForumPosts
            .Where(p => p.Id == postId && p.DeletedByAuthorAt != null && !_db.ForumComments.Any(c => c.ForumPostId == p.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (true, postRemoved == 1 ? null : postId);
    }

    // Save and hide: only another mother's post that is not deleted. Doing it twice changes nothing.
    public async Task<bool> SaveAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (await MarkablePostAsync(userId, id, cancellationToken) is not { } me)
        {
            return false;
        }

        if (!await _db.SavedPosts.AnyAsync(s => s.MotherProfileId == me && s.ForumPostId == id, cancellationToken))
        {
            _db.SavedPosts.Add(new SavedPost { MotherProfileId = me, ForumPostId = id, SavedAt = DateTime.UtcNow });
            await SaveIgnoringDuplicateAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> HideAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (await MarkablePostAsync(userId, id, cancellationToken) is not { } me)
        {
            return false;
        }

        if (!await _db.HiddenPosts.AnyAsync(h => h.MotherProfileId == me && h.ForumPostId == id, cancellationToken))
        {
            _db.HiddenPosts.Add(new HiddenPost { MotherProfileId = me, ForumPostId = id, HiddenAt = DateTime.UtcNow });
            await SaveIgnoringDuplicateAsync(cancellationToken);
        }

        return true;
    }

    // Unsave and unhide: any existing post; only her own row is removed. False when the post does not exist.
    public async Task<bool> UnsaveAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (!await _db.ForumPosts.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return false;
        }

        await _db.SavedPosts
            .Where(s => s.ForumPostId == id && _db.MotherProfiles.Any(p => p.Id == s.MotherProfileId && p.UserId == userId))
            .ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnhideAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (!await _db.ForumPosts.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return false;
        }

        await _db.HiddenPosts
            .Where(h => h.ForumPostId == id && _db.MotherProfiles.Any(p => p.Id == h.MotherProfileId && p.UserId == userId))
            .ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    // What the report page is about. Null when she may not report it: missing, her own, or (for a post) deleted
    // by its author.
    public async Task<ReportTarget?> GetPostReportTargetAsync(string userId, int postId, CancellationToken cancellationToken = default)
    {
        if (await ProfileIdAsync(userId, cancellationToken) is not { } me
            || !await ReportablePosts(me).AnyAsync(p => p.Id == postId, cancellationToken))
        {
            return null;
        }

        var already = await _db.PostReports.AnyAsync(r => r.ReporterProfileId == me && r.ForumPostId == postId, cancellationToken);
        return new ReportTarget(postId, null, already);
    }

    public async Task<ReportTarget?> GetCommentReportTargetAsync(string userId, int commentId, CancellationToken cancellationToken = default)
    {
        if (await ProfileIdAsync(userId, cancellationToken) is not { } me)
        {
            return null;
        }

        var postId = await ReportableComments(me)
            .Where(c => c.Id == commentId)
            .Select(c => (int?)c.ForumPostId)
            .SingleOrDefaultAsync(cancellationToken);
        if (postId is null)
        {
            return null;
        }

        var already = await _db.CommentReports.AnyAsync(r => r.ReporterProfileId == me && r.ForumCommentId == commentId, cancellationToken);
        return new ReportTarget(postId.Value, commentId, already);
    }

    // One report per mother per post. The post stays visible.
    public async Task<ReportResult> ReportPostAsync(
        string userId,
        int postId,
        ReportReason reason,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var (validNote, cleanNote) = CleanNote(note);
        if (!validNote
            || !Enum.IsDefined(reason)
            || await ProfileIdAsync(userId, cancellationToken) is not { } me
            || !await ReportablePosts(me).AnyAsync(p => p.Id == postId, cancellationToken))
        {
            return ReportResult.NotAllowed;
        }

        if (await _db.PostReports.AnyAsync(r => r.ReporterProfileId == me && r.ForumPostId == postId, cancellationToken))
        {
            return ReportResult.AlreadyReported;
        }

        _db.PostReports.Add(new PostReport
        {
            ReporterProfileId = me,
            ForumPostId = postId,
            Reason = reason,
            Note = cleanNote,
            Status = ReportStatus.Open,
            CreatedAt = DateTime.UtcNow
        });
        return await SaveReportAsync(cancellationToken);
    }

    // One report per mother per comment. The comment stays visible.
    public async Task<ReportResult> ReportCommentAsync(
        string userId,
        int commentId,
        ReportReason reason,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var (validNote, cleanNote) = CleanNote(note);
        if (!validNote
            || !Enum.IsDefined(reason)
            || await ProfileIdAsync(userId, cancellationToken) is not { } me
            || !await ReportableComments(me).AnyAsync(c => c.Id == commentId, cancellationToken))
        {
            return ReportResult.NotAllowed;
        }

        if (await _db.CommentReports.AnyAsync(r => r.ReporterProfileId == me && r.ForumCommentId == commentId, cancellationToken))
        {
            return ReportResult.AlreadyReported;
        }

        _db.CommentReports.Add(new CommentReport
        {
            ReporterProfileId = me,
            ForumCommentId = commentId,
            Reason = reason,
            Note = cleanNote,
            Status = ReportStatus.Open,
            CreatedAt = DateTime.UtcNow
        });
        return await SaveReportAsync(cancellationToken);
    }

    // Line breaks kept as "\n", trimmed.
    public static string CleanBody(string? text) => text?.ReplaceLineEndings("\n").Trim() ?? string.Empty;

    // One line, trimmed.
    public static string CleanTitle(string? text) => text?.ReplaceLineEndings(" ").Trim() ?? string.Empty;

    // The unique index catches a second report sent at the same moment.
    private async Task<ReportResult> SaveReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return ReportResult.Reported;
        }
        catch (DbUpdateException)
        {
            return ReportResult.AlreadyReported;
        }
    }

    // Her profile id when she may save or hide this post; null otherwise.
    private async Task<int?> MarkablePostAsync(string userId, int id, CancellationToken cancellationToken)
    {
        var me = await ProfileIdAsync(userId, cancellationToken);
        return me is not null && await ReportablePosts(me.Value).AnyAsync(p => p.Id == id, cancellationToken) ? me : null;
    }

    // A second tap at the same moment: the unique index kept one row, which is what she asked for.
    private async Task SaveIgnoringDuplicateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
        }
    }

    // Empty note -> null; longer than 300 -> not valid.
    private static (bool Valid, string? Note) CleanNote(string? note)
    {
        var clean = CleanBody(note);
        return clean.Length == 0 ? (true, null) : (clean.Length <= PostReport.NoteMaxLength, clean);
    }

    private async Task<(string Title, string Body)?> CleanPostAsync(ForumPostInput input, CancellationToken cancellationToken)
    {
        var title = CleanTitle(input.Title);
        var body = CleanBody(input.Body);
        if (title.Length == 0 || title.Length > ForumPost.TitleMaxLength
            || body.Length == 0 || body.Length > ForumPost.BodyMaxLength
            || !Enum.IsDefined(input.Language)
            || !await _db.ForumCategories.AnyAsync(c => c.Id == input.CategoryId, cancellationToken))
        {
            return null;
        }

        return (title, body);
    }

    private async Task<List<ForumPostCard>> CardsAsync(
        IQueryable<ForumPost> posts,
        IReadOnlyList<ForumCategory> categories,
        CancellationToken cancellationToken)
    {
        var rows = await posts
            .Select(p => new
            {
                p.Id,
                p.Title,
                IsDeleted = p.DeletedByAuthorAt != null,
                p.ForumCategoryId,
                AuthorName = _db.MotherProfiles.Where(m => m.Id == p.AuthorProfileId).Select(m => m.DisplayName).FirstOrDefault(),
                p.CreatedAt,
                CommentCount = _db.ForumComments.Count(c => c.ForumPostId == p.Id)
            })
            .ToListAsync(cancellationToken);

        var keys = categories.ToDictionary(c => c.Id, c => c.Key);
        return rows
            .Select(r => new ForumPostCard(r.Id, r.Title, r.IsDeleted, keys[r.ForumCategoryId], r.AuthorName, r.CreatedAt, r.CommentCount))
            .ToList();
    }

    private async Task<int?> ProfileIdAsync(string userId, CancellationToken cancellationToken) =>
        await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<ForumPost> OwnPosts(string userId) =>
        _db.ForumPosts.Where(p => _db.MotherProfiles.Any(m => m.Id == p.AuthorProfileId && m.UserId == userId));

    private IQueryable<ForumComment> OwnComments(string userId) =>
        _db.ForumComments.Where(c => _db.MotherProfiles.Any(m => m.Id == c.AuthorProfileId && m.UserId == userId));

    // Posts she may save, hide or report: another mother's (or a deleted user's), not deleted by its author.
    private IQueryable<ForumPost> ReportablePosts(int me) =>
        _db.ForumPosts.Where(p => p.DeletedByAuthorAt == null && (p.AuthorProfileId == null || p.AuthorProfileId != me));

    // Comments she may report: another mother's (or a deleted user's).
    private IQueryable<ForumComment> ReportableComments(int me) =>
        _db.ForumComments.Where(c => c.AuthorProfileId == null || c.AuthorProfileId != me);
}
