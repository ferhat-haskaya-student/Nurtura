using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The three tabs of the forum list.
public enum ForumTab
{
    All,
    Saved,
    Mine
}

// The list's filters. Hidden = true lists only the posts she hid (the tab is then ignored).
public sealed record ForumFilter(int? CategoryId, Language? Language, ForumTab Tab, bool Hidden, int Page);

// One post in a list. Times are UTC. AuthorName is null when the author's account was deleted.
public sealed record ForumPostCard(
    int Id,
    string Title,
    bool IsDeleted,
    string CategoryKey,
    string? AuthorName,
    DateTime CreatedAt,
    int CommentCount);

public sealed record ForumList(
    IReadOnlyList<ForumCategory> Categories,
    List<ForumPostCard> Posts,
    int Page,
    int TotalPages);

// One comment on the post page. Times are UTC.
public sealed record ForumCommentView(
    int Id,
    string? AuthorName,
    string Body,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsMine,
    bool CanEdit,
    bool HasReported);

// The post page. Times are UTC. Title and Body are empty when the author deleted it (IsDeleted).
public sealed record ForumPostView(
    int Id,
    string Title,
    string Body,
    string CategoryKey,
    Language Language,
    string? AuthorName,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsDeleted,
    bool IsMine,
    bool CanEdit,
    bool IsSaved,
    bool IsHidden,
    bool HasReported,
    List<ForumCommentView> Comments);

// What she typed in the post form.
public sealed record ForumPostInput(int CategoryId, Language Language, string? Title, string? Body);

// The post form's current values, for editing.
public sealed record ForumPostEdit(int Id, int CategoryId, Language Language, string Title, string Body, DateTime EditableUntil);

// A comment's current text, for editing.
public sealed record ForumCommentEdit(int Id, int PostId, string Body, DateTime EditableUntil);

public enum ForumSaveStatus
{
    Saved,

    // Empty or too long text, or an unknown category or language.
    Invalid,

    // Missing, or not hers to change.
    NotFound,

    // The 30 minutes for editing have passed.
    EditWindowPassed,

    // The post was deleted by its author; it takes no new comments.
    Closed
}

// PostId is where to go next. Urgent: her own new text contains one of the Assistant's urgent words.
public sealed record ForumSaveResult(ForumSaveStatus Status, int PostId = 0, bool Urgent = false)
{
    public static readonly ForumSaveResult Invalid = new(ForumSaveStatus.Invalid);
    public static readonly ForumSaveResult NotFound = new(ForumSaveStatus.NotFound);
    public static readonly ForumSaveResult EditWindowPassed = new(ForumSaveStatus.EditWindowPassed);
    public static readonly ForumSaveResult Closed = new(ForumSaveStatus.Closed);
}

public enum ReportResult
{
    Reported,
    AlreadyReported,

    // Missing, her own, or a post deleted by its author.
    NotAllowed
}

// What a report is about, for the report page. CommentId is null for a post.
public sealed record ReportTarget(int PostId, int? CommentId, bool AlreadyReported);
