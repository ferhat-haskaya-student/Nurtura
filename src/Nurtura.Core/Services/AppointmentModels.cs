using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// How an appointment reads in the lists. Comes from the stored status and the clock:
// Now is a confirmed appointment during its hour, Done one whose hour has ended.
public enum AppointmentState
{
    Requested,
    Confirmed,
    Now,
    Done,
    Cancelled,
    Rejected,
    Expired
}

// One appointment in the mother's lists. Date and Hour are Poland time.
// CanChange: not started yet, so she can still cancel, reschedule or edit her note.
public sealed record MotherAppointment(
    int Id,
    DateOnly Date,
    int Hour,
    Topic Topic,
    string ProfessionalName,
    string Concern,
    AppointmentState State,
    AppointmentParty? CancelledBy,
    string? Message,
    DateTime ConfirmBy,
    bool CanChange);

// Upcoming: requested or confirmed and not yet ended, soonest first. Past: everything else, newest first.
public sealed record MotherAppointments(IReadOnlyList<MotherAppointment> Upcoming, IReadOnlyList<MotherAppointment> Past);

// One appointment in the professional's lists: of the mother, only her display name, the topic and her note.
public sealed record ProfessionalAppointment(
    int Id,
    DateOnly Date,
    int Hour,
    Topic Topic,
    string MotherName,
    string Concern,
    AppointmentState State,
    AppointmentParty? CancelledBy,
    string? Message,
    DateTime ConfirmBy,
    bool CanChange);

// Requests: soonest start first. Upcoming: confirmed and not yet ended. Past: newest first.
public sealed record ProfessionalAppointments(
    IReadOnlyList<ProfessionalAppointment> Requests,
    IReadOnlyList<ProfessionalAppointment> Upcoming,
    IReadOnlyList<ProfessionalAppointment> Past);
