namespace Nurtura.Core.Entities;

// A forum post by a mother. Plain text only; line breaks are kept.
public class ForumPost
{
    public const int TitleMaxLength = 150;
    public const int BodyMaxLength = 5000;

    // The author can edit her post for this long after posting.
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(30);

    public int Id { get; set; }

    // Empty after the author's account is deleted (shown as "deleted user").
    public int? AuthorProfileId { get; set; }

    public int ForumCategoryId { get; set; }

    // Empty after the author deleted a post that has comments.
    public string Title { get; set; } = string.Empty;

    // Empty after the author deleted a post that has comments.
    public string Body { get; set; } = string.Empty;

    // The language the post is written in, so mothers can filter.
    public Language Language { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC. Empty unless edited.
    public DateTime? EditedAt { get; set; }

    // UTC. Set when the author deleted a post that has comments: title and body are cleared, the post keeps its
    // place and takes no new comments. A post without comments is removed instead.
    public DateTime? DeletedByAuthorAt { get; set; }
}
