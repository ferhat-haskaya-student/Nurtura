using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The professional's pre-consultation summary, built live from what the mother has shared right now over the
// last Days days. Only computed values leave the service: a baby's age or her postpartum stage, never a birth date.
// Date and Hour are the slot, Poland time. DaysWithData: days in the window with a shared record of the topic's kind.
// CheckIns is set for the four check-in topics, Routine for baby sleep and baby feeding (null when no baby is set).
// BabyNotes: the routine's shared sleeps (baby sleep) or feeds (baby feeding) that have a note, newest first.
public sealed record ConsultationSummary(
    int AppointmentId,
    DateOnly Date,
    int Hour,
    Topic Topic,
    string MotherName,
    string Concern,
    BabyAge? BabyAge,
    PostpartumStage? Stage,
    DateOnly From,
    DateOnly To,
    int DaysWithData,
    CheckInSummary? CheckIns,
    BabyRoutine? Routine,
    IReadOnlyList<TimelineEntry> BabyNotes)
{
    // The same two weeks as one-tap sharing.
    public const int Days = Appointment.ShareDays;
}

// Her shared check-ins: a daily chart, week 1 (the older 7 days) against week 2, her most ticked feelings,
// and the shared notes, newest first.
public sealed record CheckInSummary(
    CheckInChart Chart,
    SummaryWeek Week1,
    SummaryWeek Week2,
    IReadOnlyList<FeelingCount> TopFeelings,
    IReadOnlyList<SummaryNote> Notes);

// Averages is null when the week has no shared check-in.
public sealed record SummaryWeek(DateOnly From, DateOnly To, int CheckInCount, RatingAverages? Averages);

public sealed record SummaryNote(DateOnly Date, string Text);
