using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Nurtura.Core;

namespace Nurtura.Tests.Infrastructure;

// The fields of the sleep timer form, posted the same way to the form (/Mother/Sleep/Save) and to the API (/api/sleep).
public static partial class SleepForm
{
    // One press of a button the rules refuse. Message: the start of the English message she sees.
    public sealed record Refusal(string Command, Dictionary<string, string> Fields, string Message);

    public static readonly string[] RefusalNames =
    [
        "already running", "overlap", "too short", "resume after an hour", "in the future", "too long", "no start date",
        "wrong start date"
    ];

    public static string DateText(DateTime utc) =>
        PolandTime.ToLocal(utc).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    public static string TimeText(DateTime utc) =>
        PolandTime.ToLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static Dictionary<string, string> Fields(int babyId, int? sleepId, DateTime startUtc, DateTime? endUtc = null) => new()
    {
        ["babyId"] = babyId.ToString(),
        ["sleepId"] = sleepId?.ToString() ?? "",
        ["DefaultStart"] = "",
        ["StartDate"] = DateText(startUtc),
        ["StartTime"] = TimeText(startUtc),
        ["EndDate"] = endUtc is null ? "" : DateText(endUtc.Value),
        ["EndTime"] = endUtc is null ? "" : TimeText(endUtc.Value),
        ["Note"] = "",
        ["IsShared"] = "false"
    };

    // The form as it is when she has not touched the start: "now" when the page was opened.
    public static Dictionary<string, string> FieldsLeftAtNow(int babyId, DateTime openedUtc)
    {
        var fields = Fields(babyId, null, openedUtc);
        fields["DefaultStart"] = DateText(openedUtc) + " " + TimeText(openedUtc);
        return fields;
    }

    // Puts the sleeps in the database that make the rules refuse the press.
    public static async Task<Refusal> ArrangeAsync(TestData data, int babyId, string name)
    {
        var now = DateTime.UtcNow;
        switch (name)
        {
            case "already running":
                await data.AddRunningSleepAsync(babyId, now.AddMinutes(-30));
                return new("start", Fields(babyId, null, now), "Another sleep is already running for this baby.");
            case "overlap":
                await data.AddSleepAsync(babyId, now.AddHours(-3), TimeSpan.FromHours(1), null, false);
                return new("start", Fields(babyId, null, now.AddHours(-2.5)), "This overlaps another sleep: ");
            case "too short":
                await AwayFromMinuteEndAsync();
                now = DateTime.UtcNow;
                return new("stop", Fields(babyId, await data.AddRunningSleepAsync(babyId, now), now),
                    "This sleep is shorter than a minute");
            case "resume after an hour":
                var start = now.AddMinutes(-120);
                var stopped = await data.AddSleepAsync(babyId, start, TimeSpan.FromMinutes(30), null, false);
                return new("resume", Fields(babyId, stopped, start, start.AddMinutes(30)),
                    "This sleep ended too long ago to resume.");
            case "in the future":
                return new("start", Fields(babyId, null, now.AddHours(1)), "The times can't be in the future.");
            case "too long":
                var running = await data.AddRunningSleepAsync(babyId, now.AddHours(-25));
                return new("stop", Fields(babyId, running, now.AddHours(-25)), "This sleep is longer than 24 hours.");
            case "no start date":
                var empty = Fields(babyId, null, now);
                empty["StartDate"] = "";
                return new("start", empty, "The Start date field is required.");
            case "wrong start date":
                var wrong = Fields(babyId, null, now);
                wrong["StartDate"] = "2026/11/24";
                return new("start", wrong, "Enter the date as dd-mm-yyyy");
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }

    // Times are kept to the minute: a test that needs two calls in one minute waits for a new one near its end.
    public static async Task AwayFromMinuteEndAsync()
    {
        while (DateTime.UtcNow.Second >= 50)
        {
            await Task.Delay(500);
        }
    }

    // Every message the form shows: the summary on top, then the ones under the fields.
    public static List<string> Messages(string html) => MessagePattern().Matches(html)
        .Select(m => WebUtility.HtmlDecode(m.Groups["text"].Value).Trim())
        .Where(t => t.Length > 0)
        .ToList();

    [GeneratedRegex("""(validation-summary-errors[^>]*>\s*(<ul>)?\s*<li>|field-validation-error[^>]*>)(?<text>[^<]*)<""")]
    private static partial Regex MessagePattern();
}
