using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// How an appointment reads in the lists. Comes from the stored status and the clock:
// Now is a confirmed appointment during its session, Done one whose session has ended (end of the hour,
// or earlier after End session).
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

// The chat button on an appointment. None: no chat (not confirmed). NotYetOpen: greyed, "Opens at 13:55".
// Open: "Open chat". ViewOnly: the session has ended, "View chat".
public enum ChatAccess
{
    None,
    NotYetOpen,
    Open,
    ViewOnly
}

// One appointment in the mother's lists. Date, Hour and ChatOpensAt are Poland time.
// CanCancel: requested or confirmed and the chat room has not opened, so she can still cancel or reschedule.
// CanEditNote: requested or confirmed and not started, so she can still edit her note.
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
    bool CanCancel,
    bool CanEditNote,
    ChatAccess Chat,
    DateTime ChatOpensAt);

// Upcoming: requested or confirmed and not yet ended, soonest first. Past: everything else, newest first.
public sealed record MotherAppointments(IReadOnlyList<MotherAppointment> Upcoming, IReadOnlyList<MotherAppointment> Past);

// One appointment in the professional's lists: of the mother, only her display name, the topic and her note.
// CanCancel: as for the mother (accept and reject are for requests, which expire well before the room opens).
// NeedsClosingNote: the session has ended and she has not written the closing note yet (still Confirmed).
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
    bool CanCancel,
    bool CanEditNote,
    ChatAccess Chat,
    DateTime ChatOpensAt,
    bool NeedsClosingNote);

// A confirmed consultation from 24 hours before its start until its end (end of the hour, or End session).
// Hour and OpensAt are Poland time. IsNow: during the session. IsToday: today, otherwise tomorrow.
// RoomOpen: the chat room is open (from 5 minutes before the start).
public sealed record ConsultationBanner(
    int AppointmentId,
    string OtherName,
    int Hour,
    bool IsNow,
    bool IsToday,
    bool RoomOpen,
    DateTime OpensAt);

// Requests: soonest start first. Upcoming: confirmed and not yet ended. Past: newest first.
public sealed record ProfessionalAppointments(
    IReadOnlyList<ProfessionalAppointment> Requests,
    IReadOnlyList<ProfessionalAppointment> Upcoming,
    IReadOnlyList<ProfessionalAppointment> Past);
