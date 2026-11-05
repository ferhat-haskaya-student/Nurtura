namespace Nurtura.Core.Entities;

// Stored as text, so renaming a value needs a migration.
// Upcoming and In progress are not stored: they come from comparing the slot time to the clock.
public enum AppointmentStatus
{
    Requested,
    Confirmed,
    Rejected,
    Completed,
    Cancelled,

    // A request the professional did not confirm by 2 hours before the start.
    Expired
}
