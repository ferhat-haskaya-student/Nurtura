namespace Nurtura.Core.Entities;

// Where a forum report stands. New reports are Open; the admin review screen (later) sets the others.
// Stored as text, so renaming a value needs a migration.
public enum ReportStatus
{
    Open,
    Dismissed,
    Actioned
}
