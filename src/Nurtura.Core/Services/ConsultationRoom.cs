using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The chat room of a confirmed appointment, worked out from the clock. All times are Poland time.
// The real end of a session is the end of its hour, or earlier when either side used End session;
// the banner, "Now", the summary, the lists and the chat all use this one definition.
public static class ConsultationRoom
{
    public static DateTime Start(DateOnly date, int hour) => BookingService.SlotStart(date, hour);

    // The room opens 5 minutes before the start. From then on, Cancel and Reschedule are gone for both sides.
    public static DateTime OpensAt(DateOnly date, int hour) => Start(date, hour) - Appointment.RoomOpensBefore;

    // The end of the hour, or the moment End session was used if that came first.
    public static DateTime End(DateOnly date, int hour, DateTime? sessionEndedAtUtc)
    {
        var endOfHour = Start(date, hour) + Appointment.Length;
        if (sessionEndedAtUtc is null)
        {
            return endOfHour;
        }

        var endedAt = PolandTime.ToLocal(sessionEndedAtUtc.Value);
        return endedAt < endOfHour ? endedAt : endOfHour;
    }

    public static RoomState StateOf(DateOnly date, int hour, DateTime? sessionEndedAtUtc, DateTime now)
    {
        if (now >= End(date, hour, sessionEndedAtUtc))
        {
            return RoomState.Closed;
        }

        return now >= OpensAt(date, hour) ? RoomState.Open : RoomState.NotYetOpen;
    }

    // The room has not opened yet, so the appointment can still be cancelled or rescheduled.
    public static bool BeforeOpening(DateOnly date, int hour, DateTime now) => now < OpensAt(date, hour);

    // For SQL filters: a slot (starting on the full hour) opens its room after now exactly when its date is later
    // than CutoffDate, or the same and its hour later than CutoffHour.
    public static (DateOnly CutoffDate, int CutoffHour) OpeningCutoff(DateTime now)
    {
        var cutoff = now + Appointment.RoomOpensBefore;
        return (DateOnly.FromDateTime(cutoff), cutoff.Hour);
    }
}

public enum RoomState
{
    NotYetOpen,
    Open,
    Closed
}
