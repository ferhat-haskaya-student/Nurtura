namespace Nurtura.Core.Entities;

// A comment under a forum post. One flat list per post, oldest first. Plain text only; line breaks are kept.
public class ForumComment
{
    public const int BodyMaxLength = 2000;

    // The author can edit her comment for this long after posting.
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(30);

    public int Id { get; set; }

    public int ForumPostId { get; set; }

    // Empty after the author's account is deleted (shown as "deleted user").
    public int? AuthorProfileId { get; set; }

    public string Body { get; set; } = string.Empty;

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC. Empty unless edited.
    public DateTime? EditedAt { get; set; }
}
