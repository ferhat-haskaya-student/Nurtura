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
// Babies are hers, filled only for the baby sleep and baby feeding topics.
public sealed record BookingRequestPage(FreeSlot Slot, BookingStatus? Problem, IReadOnlyList<Baby> Babies);

public sealed record BookingRequest(int SlotId, Topic Topic, string Concern, bool ShareCheckIns, int? ShareBabyId);

public enum BookingStatus
{
    Sent,

    // Requested by someone else, closed, or no longer in the 12 hours to 4 weeks window.
    SlotTaken,

    // She already has the maximum number of upcoming appointments.
    TooMany,

    // She already has an appointment at that date and hour.
    SameTime,

    Invalid
}

// On Sent: the professional's name and the Poland time she has to confirm by.
public sealed record BookingResult(BookingStatus Status, string? ProfessionalName = null, DateTime? ConfirmBy = null);
