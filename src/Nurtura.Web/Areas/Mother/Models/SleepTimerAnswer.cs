namespace Nurtura.Web.Areas.Mother.Models;

// What the sleep timer API answers, as JSON: everything wwwroot/js/sleep-timer-api.js needs to show the screen's state.
// Built from the database, never from the request. Texts are in the current language, dates are dd-mm-yyyy and times
// HH:mm in Warsaw time, moments are UTC milliseconds.
public sealed record SleepTimerAnswer
{
    // Null for a sleep that has not been started; so are EditUrl and ResetUrl.
    public int? SleepId { get; init; }

    // The big round button; null when there is none.
    public string? Command { get; init; }

    public string? CommandText { get; init; }

    // Set while it runs, for the ticking time.
    public long? RunningSince { get; init; }

    // Set when stopped: "7 h 13 min".
    public string? DurationText { get; init; }

    public string? StartDate { get; init; }

    public string? StartTime { get; init; }

    public string? EndDate { get; init; }

    public string? EndTime { get; init; }

    public string? Note { get; init; }

    public bool IsShared { get; init; }

    public bool IsLongRunning { get; init; }

    public string? EditUrl { get; init; }

    public string? ResetUrl { get; init; }

    public long ServerNow { get; init; }
}
