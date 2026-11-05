namespace Nurtura.Core.Entities;

// A mother's request for one of a professional's slots. The professional, date and hour come from the slot.
public class Appointment
{
    public const int ConcernMaxLength = 500;

    // A slot can be requested from this long before its start...
    public static readonly TimeSpan MinNotice = TimeSpan.FromHours(12);

    // ...up to this many days ahead.
    public const int MaxDaysAhead = 28;

    // A request not confirmed by this long before the start expires and frees the slot.
    public static readonly TimeSpan ConfirmBefore = TimeSpan.FromHours(2);

    // At most this many upcoming (requested or confirmed) appointments per mother.
    public const int MaxUpcoming = 3;

    // "Mark ... from the last 2 weeks as shared": today and the 13 days before it.
    public const int ShareDays = 14;

    // Only one active appointment per slot (also enforced by a partial unique index).
    public static readonly AppointmentStatus[] ActiveStatuses = [AppointmentStatus.Requested, AppointmentStatus.Confirmed];

    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public int AvailabilitySlotId { get; set; }

    public Topic Topic { get; set; }

    // "What would you like to talk about?"
    public string ConcernText { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC.
    public DateTime UpdatedAt { get; set; }
}
