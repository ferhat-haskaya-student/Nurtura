namespace Nurtura.Core.Entities;

// One side of an appointment: who cancelled it, who ended the session, who sent a chat message. Stored as text.
public enum AppointmentParty
{
    Mother,
    Professional,

    // Only for CancelledBy: the admin deactivated the professional, which cancelled her upcoming appointments.
    Nurtura
}
