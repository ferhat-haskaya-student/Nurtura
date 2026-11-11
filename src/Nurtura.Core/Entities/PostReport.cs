namespace Nurtura.Core.Entities;

// A mother's report of a forum post. One per mother per post. The post stays visible.
public class PostReport
{
    public const int NoteMaxLength = 300;

    public int Id { get; set; }

    public int ReporterProfileId { get; set; }

    public int ForumPostId { get; set; }

    public ReportReason Reason { get; set; }

    public string? Note { get; set; }

    public ReportStatus Status { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }
}
