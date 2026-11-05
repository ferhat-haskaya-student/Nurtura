namespace Nurtura.Core.Entities;

// One one-hour slot a professional has set up. Slots start on the full hour, Poland time,
// from 08:00 to 17:00 (the last one is 17:00–18:00). One row per professional per date and hour.
// Closing an hour sets IsOpen to false instead of deleting the row, so a slot that an appointment
// points to (added in step 5, piece 2) is never removed.
public class AvailabilitySlot
{
    public const int FirstHour = 8;
    public const int LastHour = 17;

    // She can set the current week and this many weeks after it.
    public const int WeeksAhead = 4;

    // Monday to Saturday. Sunday is a day off: never shown, opened or booked.
    public const int WorkDaysPerWeek = 6;

    public static bool IsDayOff(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday;

    public int Id { get; set; }

    public int ProfessionalProfileId { get; set; }

    // Poland date.
    public DateOnly Date { get; set; }

    // Poland time, full hour (8 to 17).
    public int StartHour { get; set; }

    public bool IsOpen { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC.
    public DateTime UpdatedAt { get; set; }
}
