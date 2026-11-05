using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The short profile shown with each free slot.
public sealed record ProfessionalCard(int Id, string DisplayName, ProfessionalType Type, IReadOnlyList<Language> Languages);

// The full profile page.
public sealed record ProfessionalDetails(
    int Id,
    string DisplayName,
    ProfessionalType Type,
    IReadOnlyList<Topic> Topics,
    IReadOnlyList<Language> Languages,
    string Bio);

// Date and Hour are Poland time; the session runs from Hour to Hour + 1.
public sealed record FreeSlot(int SlotId, DateOnly Date, int Hour, ProfessionalCard Professional);

public sealed record FreeSlotDay(DateOnly Date, IReadOnlyList<FreeSlot> Slots);

// The request page. Problem is set when she cannot request this slot (it is then shown instead of the form).
// Babies are all of hers, youngest last: for baby topics she picks the one the consultation is about;
// for other topics they only tell whether a postpartum stage exists.
public sealed record BookingRequestPage(FreeSlot Slot, BookingStatus? Problem, IReadOnlyList<Baby> Babies);

// BabyId: for baby topics, the baby the consultation is about (required when she has babies); null otherwise.
// ShareBaby: also share that baby's sleeps and feeds. RescheduleId: her appointment that this request replaces;
// it is cancelled when the request is sent.
public sealed record BookingRequest(
    int SlotId,
    Topic Topic,
    string Concern,
    bool ShareCheckIns,
    int? BabyId,
    bool ShareBaby,
    int? RescheduleId = null);

// The appointment she is rescheduling: requested or confirmed, not started yet. Date and Hour are Poland time.
public sealed record RescheduleInfo(
    int AppointmentId,
    DateOnly Date,
    int Hour,
    Topic Topic,
    int ProfessionalId,
    string ProfessionalName,
    bool WasConfirmed,
    string Concern,
    int? BabyId);

public enum BookingStatus
{
    Sent,

    // Requested by someone else, closed, or no longer in the 12 hours to 4 weeks window.
    SlotTaken,

    // She already has the maximum number of upcoming appointments.
    TooMany,

    // She already has an appointment at that date and hour.
    SameTime,

    // The appointment being rescheduled has started, or was cancelled, rejected or expired meanwhile.
    RescheduleGone,

    Invalid
}

// On Sent: the professional's name and the Poland time she has to confirm by.
public sealed record BookingResult(BookingStatus Status, string? ProfessionalName = null, DateTime? ConfirmBy = null);
