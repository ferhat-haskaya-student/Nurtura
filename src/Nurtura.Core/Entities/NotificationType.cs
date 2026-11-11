namespace Nurtura.Core.Entities;

// What happened. Stored as text, so renaming a value needs a migration.
public enum NotificationType
{
    // To the mother.
    RequestAccepted,
    RequestRejected,
    CancelledByProfessional,
    RequestExpired,
    ClosingNoteWritten,
    CommentOnYourPost,

    // To the professional.
    NewRequest,
    CancelledByMother,
    Rescheduled
}
