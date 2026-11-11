namespace Nurtura.Core.Entities;

// A mother's report of a forum comment. One per mother per comment. The comment stays visible.
public class CommentReport
{
    public const int NoteMaxLength = 300;

    public int Id { get; set; }

    public int ReporterProfileId { get; set; }

    public int ForumCommentId { get; set; }

    public ReportReason Reason { get; set; }

    public string? Note { get; set; }

    public ReportStatus Status { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }
}
