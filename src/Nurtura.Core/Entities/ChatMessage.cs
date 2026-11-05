namespace Nurtura.Core.Entities;

// One message in the consultation chat of a confirmed appointment. Text only; never deleted.
public class ChatMessage
{
    public const int TextMaxLength = 1000;

    // The sender can edit a message for this long after sending, and only while the room is open.
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(3);

    public int Id { get; set; }

    public int AppointmentId { get; set; }

    // Who sent it: the appointment has only these two people, so their names come from the appointment.
    public AppointmentParty Sender { get; set; }

    public string Text { get; set; } = string.Empty;

    // UTC.
    public DateTime SentAt { get; set; }

    // UTC. Empty unless edited.
    public DateTime? EditedAt { get; set; }
}
