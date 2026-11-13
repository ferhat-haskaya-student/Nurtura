namespace Nurtura.Core.Entities;

// An in-app notification. Only the details are stored; the sentence is built when it is shown,
// in the reader's current language.
public class Notification
{
    // Read notifications older than this are deleted.
    public const int KeepReadDays = 30;

    public int Id { get; set; }

    // The user who receives it.
    public string UserId { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    // Appointment notifications: the appointment; for Rescheduled, the new one.
    public int? AppointmentId { get; set; }

    // CommentOnYourPost: the post and its title when the comment was written.
    public int? ForumPostId { get; set; }

    public string? PostTitle { get; set; }

    // CommentOnYourPost: the comment that caused it, so deleting the comment (or its author's account) deletes the
    // notification. Empty for notifications written before this link existed.
    public int? ForumCommentId { get; set; }

    // The other side's display name when it happened (for CommentOnYourPost, the commenter).
    public string OtherName { get; set; } = string.Empty;

    // Appointment notifications: the slot, Poland time.
    public DateOnly? Date { get; set; }

    public int? Hour { get; set; }

    // Rescheduled only: the old slot, Poland time.
    public DateOnly? PreviousDate { get; set; }

    public int? PreviousHour { get; set; }

    // The professional's optional message (rejected or cancelled).
    public string? Message { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
}
