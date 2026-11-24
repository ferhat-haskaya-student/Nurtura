using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// The ticking clock starts at 0:00:00: it counts from the exact tap on Start (TimerStartedAt), kept while the start
// minute stays. Everything else uses the minute times. Through the form and the API alike.
public sealed partial class SleepClockTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private const string Form = "form";
    private const string Api = "api";

    public static TheoryData<string> Ways => new(Form, Api);

    // Presses a button through the form or the API. Returns the sleep as it is now in the database.
    private async Task<SleepRecord> PressAsync(AppClient client, string way, string command, Dictionary<string, string> fields)
    {
        if (way == Form)
        {
            var response = await client.PostAsync("/Mother/Sleep/Save", new(fields) { ["command"] = command });
            AssertRedirect(response, command == "save" ? "/Mother/Baby" : "/Mother/Sleep/Edit/");
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostApiAsync("/api/sleep/" + command, fields)).StatusCode);
        }

        return await OnlySleepAsync();
    }

    private Task<SleepRecord> OnlySleepAsync() =>
        Data.DbAsync(db => db.SleepRecords.AsNoTracking().SingleAsync());

    private static long Milliseconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();

    // Between second 10 and 45, so a clock that counts from the minute is clearly not at 0:00:00.
    private static async Task MidMinuteAsync()
    {
        while (DateTime.UtcNow.Second is < 10 or > 45)
        {
            await Task.Delay(500);
        }
    }

    private static long RunningSince(string html)
    {
        var match = RunningSincePattern().Match(html);
        Assert.True(match.Success, "No ticking clock on the page.");
        return long.Parse(match.Groups[1].Value);
    }

    [GeneratedRegex("data-running-since=\"(\\d+)\"")]
    private static partial Regex RunningSincePattern();

    [GeneratedRegex("data-server-now=\"(\\d+)\"")]
    private static partial Regex ServerNowPattern();

    // The clock's text as the server writes it, before the script ticks.
    [GeneratedRegex("data-format=\"clock\">([^<]*)<")]
    private static partial Regex ClockTextPattern();

    // A running sleep whose Start was tapped 37 seconds into its minute.
    private async Task<(int Id, DateTime Start, DateTime Tapped)> TappedSleepAsync(int minutesAgo = 40)
    {
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-minutesAgo));
        var tapped = start.AddSeconds(37);
        return (await Data.AddRunningSleepAsync(Users.A.BabyId, start, tapped), start, tapped);
    }

    // ---- Start

    [Theory]
    [MemberData(nameof(Ways))]
    public async Task Start_LeftAtNow_ClockStartsAtZero(string way)
    {
        using var a = await SignInAsync(Users.A.Email);
        await MidMinuteAsync();
        var fields = SleepForm.FieldsLeftAtNow(Users.A.BabyId, DateTime.UtcNow.AddMinutes(-5));
        var before = DateTime.UtcNow;

        var sleep = await PressAsync(a, way, "start", fields);

        var after = DateTime.UtcNow;
        // PostgreSQL keeps microseconds, so the stored moment can be a fraction of one earlier.
        Assert.InRange(sleep.TimerStartedAt!.Value, before.AddMilliseconds(-1), after);
        Assert.Equal(TestData.Minute(sleep.TimerStartedAt.Value), sleep.StartTime);
        Assert.NotEqual(sleep.StartTime, sleep.TimerStartedAt.Value);

        var html = await a.GetHtmlAsync($"/Mother/Sleep/Edit/{sleep.Id}");
        Assert.Equal(Milliseconds(sleep.TimerStartedAt.Value), RunningSince(html));
        Assert.InRange(long.Parse(ServerNowPattern().Match(html).Groups[1].Value) - RunningSince(html), 0, 2000);
        Assert.Contains(ClockTextPattern().Match(html).Groups[1].Value, new[] { "0:00:00", "0:00:01", "0:00:02" });
    }

    [Fact]
    public async Task Start_LeftAtNow_ApiAnswersTheTap()
    {
        using var a = await SignInAsync(Users.A.Email);
        await MidMinuteAsync();

        var response = await a.PostApiAsync("/api/sleep/start", SleepForm.FieldsLeftAtNow(Users.A.BabyId, DateTime.UtcNow));

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var since = json.GetProperty("runningSince").GetInt64();
        Assert.Equal(Milliseconds((await OnlySleepAsync()).TimerStartedAt!.Value), since);
        Assert.InRange(json.GetProperty("serverNow").GetInt64() - since, 0, 2000);
    }

    [Theory]
    [MemberData(nameof(Ways))]
    public async Task Start_SetByHand_ClockCountsFromTheMinute(string way)
    {
        using var a = await SignInAsync(Users.A.Email);
        var now = DateTime.UtcNow;
        var fields = SleepForm.Fields(Users.A.BabyId, null, now.AddMinutes(-10));
        fields["DefaultStart"] = SleepForm.DateText(now) + " " + SleepForm.TimeText(now);

        var sleep = await PressAsync(a, way, "start", fields);

        Assert.Null(sleep.TimerStartedAt);
        Assert.Equal(Milliseconds(sleep.StartTime), RunningSince(await a.GetHtmlAsync($"/Mother/Sleep/Edit/{sleep.Id}")));
    }

    // Both times by hand, the start left at the "now" of the page: Save is not a tap on Start.
    [Fact]
    public async Task PastSleep_SetByHand_HasNoTap()
    {
        using var a = await SignInAsync(Users.A.Email);
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-30));
        var fields = SleepForm.Fields(Users.A.BabyId, null, start, start.AddMinutes(20));
        fields["DefaultStart"] = SleepForm.DateText(start) + " " + SleepForm.TimeText(start);

        var sleep = await PressAsync(a, Form, "save", fields);

        Assert.Null(sleep.TimerStartedAt);
        Assert.Equal(start, sleep.StartTime);
        Assert.Equal(start.AddMinutes(20), sleep.EndTime);
    }

    // ---- Kept

    [Theory]
    [MemberData(nameof(Ways))]
    public async Task StopAndResume_KeepTheTap(string way)
    {
        using var a = await SignInAsync(Users.A.Email);
        var (id, start, tapped) = await TappedSleepAsync();

        var stopped = await PressAsync(a, way, "stop", SleepForm.Fields(Users.A.BabyId, id, start));
        Assert.NotNull(stopped.EndTime);
        Assert.Equal(tapped, stopped.TimerStartedAt);

        // The duration is still by the minute, and nothing ticks.
        var html = await a.GetHtmlAsync($"/Mother/Sleep/Edit/{id}");
        Assert.DoesNotMatch(RunningSincePattern(), html);
        Assert.Contains($">{(stopped.EndTime!.Value - start).TotalMinutes} min<", html);

        var resumed = await PressAsync(a, way, "resume", SleepForm.Fields(Users.A.BabyId, id, start, stopped.EndTime));
        Assert.Null(resumed.EndTime);
        Assert.Equal(start, resumed.StartTime);
        Assert.Equal(tapped, resumed.TimerStartedAt);
        Assert.Equal(Milliseconds(tapped), RunningSince(await a.GetHtmlAsync($"/Mother/Sleep/Edit/{id}")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_OnlyNoteOrEndChanged_KeepsTheTap(bool withEnd)
    {
        using var a = await SignInAsync(Users.A.Email);
        var (id, start, tapped) = await TappedSleepAsync();
        var note = TestData.Marker("SleepNote");
        var fields = SleepForm.Fields(Users.A.BabyId, id, start, withEnd ? start.AddMinutes(20) : null);
        fields["Note"] = note;

        var sleep = await PressAsync(a, Form, "save", fields);

        Assert.Equal(note, sleep.Note);
        Assert.Equal(start, sleep.StartTime);
        Assert.Equal(tapped, sleep.TimerStartedAt);
    }

    // Stop refused (longer than 24 hours), with the start changed too: nothing of it is saved.
    [Fact]
    public async Task Refused_KeepsTheTap()
    {
        using var a = await SignInAsync(Users.A.Email);
        var (id, start, _) = await TappedSleepAsync(minutesAgo: 25 * 60);
        var before = await Data.SleepSnapshotAsync();

        var response = await a.PostApiAsync("/api/sleep/stop", SleepForm.Fields(Users.A.BabyId, id, start.AddMinutes(-5)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    // ---- Cleared

    [Fact]
    public async Task Save_StartChanged_ClockCountsFromTheMinute()
    {
        using var a = await SignInAsync(Users.A.Email);
        var (id, start, _) = await TappedSleepAsync();

        var sleep = await PressAsync(a, Form, "save", SleepForm.Fields(Users.A.BabyId, id, start.AddMinutes(-5)));

        Assert.Null(sleep.TimerStartedAt);
        Assert.Equal(start.AddMinutes(-5), sleep.StartTime);
        Assert.Equal(Milliseconds(sleep.StartTime), RunningSince(await a.GetHtmlAsync($"/Mother/Sleep/Edit/{id}")));

        // Set back to the old minute by hand: still by hand.
        Assert.Null((await PressAsync(a, Form, "save", SleepForm.Fields(Users.A.BabyId, id, start))).TimerStartedAt);
    }

    [Theory]
    [MemberData(nameof(Ways))]
    public async Task Stop_StartChanged_ThenResume_ClockCountsFromTheMinute(string way)
    {
        using var a = await SignInAsync(Users.A.Email);
        var (id, start, _) = await TappedSleepAsync();

        var stopped = await PressAsync(a, way, "stop", SleepForm.Fields(Users.A.BabyId, id, start.AddMinutes(1)));
        Assert.Null(stopped.TimerStartedAt);

        var fields = SleepForm.Fields(Users.A.BabyId, id, stopped.StartTime, stopped.EndTime);
        Assert.Null((await PressAsync(a, way, "resume", fields)).TimerStartedAt);
        Assert.Equal(Milliseconds(start.AddMinutes(1)), RunningSince(await a.GetHtmlAsync($"/Mother/Sleep/Edit/{id}")));
    }

    // ---- The baby page, and what stays by the minute

    [Fact]
    public async Task BabyTile_CountsFromTheTap()
    {
        using var a = await SignInAsync(Users.A.Email);
        var (_, _, tapped) = await TappedSleepAsync();

        Assert.Equal(Milliseconds(tapped), RunningSince(await a.GetHtmlAsync("/Mother/Baby")));
    }

    [Fact]
    public async Task BabyTile_StartSetByHand_CountsFromTheMinute()
    {
        using var a = await SignInAsync(Users.A.Email);
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-40));
        await Data.AddRunningSleepAsync(Users.A.BabyId, start);

        Assert.Equal(Milliseconds(start), RunningSince(await a.GetHtmlAsync("/Mother/Baby")));
    }

    // 12 hours by the minute, less than 12 hours since the tap.
    [Fact]
    public async Task LongRunningNote_StaysByTheMinute()
    {
        using var a = await SignInAsync(Users.A.Email);
        await SleepForm.AwayFromMinuteEndAsync();
        var start = TestData.Minute(DateTime.UtcNow.AddHours(-12));
        var id = await Data.AddRunningSleepAsync(Users.A.BabyId, start, start.AddSeconds(59.9));

        Assert.Contains("over 12 hours", await a.GetHtmlAsync("/Mother/Baby"));
        Assert.Contains("class=\"alert alert-info\" role=\"status\" data-sleep-long-running", await a.GetHtmlAsync($"/Mother/Sleep/Edit/{id}"));
    }
}
