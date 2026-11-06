namespace Nurtura.Core.Entities;

// The professional's closing note after a consultation: one per appointment, plain text.
// Written only after the session has ended; the first save sets the appointment to Completed.
public class ConsultationNote
{
    public const int TextMaxLength = 2000;

    // She can edit it for this long after first saving it; after that it is read-only for both sides.
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    public int Id { get; set; }

    public int AppointmentId { get; set; }

    public string Text { get; set; } = string.Empty;

    // UTC. The first save; the edit window counts from here.
    public DateTime CreatedAt { get; set; }

    // UTC. The last save.
    public DateTime UpdatedAt { get; set; }
}
