using System.Net;
using System.Text.Json;
using Nurtura.Core;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// The clock on the timer screen follows the picked times before anything is saved (wwwroot/js/sleep-timer.js). The real
// page with its real scripts, in Edge: times are picked with the wheels. EdgePage stops the time 37 seconds into a minute.
public sealed class SleepPickerClockTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private EdgePage Edge(string html) => new(Factory, html);

    private async Task<string> HtmlAsync(string url, string language = "en")
    {
        using var a = await SignInAsync(Users.A.Email);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.AcceptLanguage.ParseAdd(language);
        var response = await a.Http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private Task<string> NewHtmlAsync(string language = "en") => HtmlAsync($"/Mother/Sleep/Timer/{Users.A.BabyId}", language);

    private Task<string> SleepHtmlAsync(int id, string language = "en") => HtmlAsync($"/Mother/Sleep/Edit/{id}", language);

    // What is picked: "24-11-2026 18:57", Warsaw time.
    private static string Field(DateTime utc) => SleepForm.DateText(utc) + " " + SleepForm.TimeText(utc);

    // "5:00:37"
    private static string Clock(TimeSpan time) => $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";

    private static string Length(TimeSpan time) =>
        time.Hours == 0 ? $"{time.Minutes} min" : time.Minutes == 0 ? $"{time.Hours} h" : $"{time.Hours} h {time.Minutes} min";

    private static TimeSpan Seconds(int seconds) => TimeSpan.FromSeconds(seconds);

    // ---- A new sleep

    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    public async Task New_ClockFollowsTheFields(string language)
    {
        var edge = Edge(await NewHtmlAsync(language));
        var minute = TestData.Minute(edge.ServerNow);
        var start = minute.AddHours(-5);
        var early = minute.AddHours(-8).AddMinutes(-13);
        var end = minute.AddHours(-1);

        var seen = await edge.RunAsync<string[]>($$"""
            var seen = [clock()];
            await advance(3000);
            seen.push(clock());
            await pick('start', '{{Field(start)}}');
            seen.push(clock());
            await advance(4000);
            seen.push(clock());
            await pick('end', '{{Field(end)}}');
            seen.push(clock());
            await pick('start', '{{Field(early)}}');
            seen.push(clock());
            await advance(5000);
            seen.push(clock());
            await clear('end');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await pick('start', document.querySelector('[name=DefaultStart]').value);
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            return seen;
            """);

        Assert.Equal(
            [
                // Left at "now".
                "0:00:00", "0:00:00",
                // 5 hours ago, 37 + 3 seconds into the minute. Not running: stands still.
                "5:00:40", "5:00:40",
                // With an end: the length, which stands still.
                "4 h", "7 h 13 min", "7 h 13 min",
                // The end cleared.
                "8:13:49", "8:13:49",
                // Picked back to its "now".
                "0:00:00", "0:00:00"
            ],
            seen);
    }

    // ---- A running sleep

    [Fact]
    public async Task Running_ClockFollowsTheFields()
    {
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-40));
        var tapped = start.AddSeconds(20);
        var id = await Data.AddRunningSleepAsync(Users.A.BabyId, start, tapped);
        var edge = Edge(await SleepHtmlAsync(id));
        var now = edge.ServerNow;
        var earlier = start.AddHours(-2);
        var end = start.AddMinutes(25);

        var seen = await edge.RunAsync<string[]>($$"""
            var seen = [clock()];
            await advance(2000);
            seen.push(clock());
            await pick('start', '{{Field(earlier)}}');
            seen.push(clock());
            await advance(3000);
            seen.push(clock());
            await pick('end', '{{Field(end)}}');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await clear('end');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await pick('start', '{{Field(start)}}');
            seen.push(clock());
            return seen;
            """);

        Assert.Equal(
            [
                // From the exact tap, as the server gave it.
                Clock(now - tapped), Clock(now - tapped + Seconds(2)),
                // From the picked minute.
                Clock(now - earlier + Seconds(2)), Clock(now - earlier + Seconds(5)),
                "2 h 25 min", "2 h 25 min",
                Clock(now - earlier + Seconds(6)), Clock(now - earlier + Seconds(7)),
                // The saved minute again: the exact tap.
                Clock(now - tapped + Seconds(7))
            ],
            seen);
    }

    // ---- A stopped sleep

    [Fact]
    public async Task Stopped_ClockFollowsTheFields()
    {
        var start = TestData.Minute(DateTime.UtcNow.AddHours(-6));
        var id = await Data.AddSleepAsync(Users.A.BabyId, start, TimeSpan.FromHours(2), null, false);
        var edge = Edge(await SleepHtmlAsync(id));
        var now = edge.ServerNow;

        var seen = await edge.RunAsync<string[]>($$"""
            var seen = [clock()];
            await pick('end', '{{Field(start.AddMinutes(45))}}');
            seen.push(clock());
            await pick('start', '{{Field(start.AddHours(-3))}}');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await clear('end');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await pick('start', '{{Field(start)}}');
            seen.push(clock());
            await pick('end', '{{Field(start.AddHours(3))}}');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            return seen;
            """);

        Assert.Equal(
            [
                "2 h", "45 min", "3 h 45 min", "3 h 45 min",
                // The end cleared: the time since the start, which stands still.
                Clock(now - start.AddHours(-3) + Seconds(1)), Clock(now - start.AddHours(-3) + Seconds(1)),
                Clock(now - start + Seconds(2)),
                "3 h", "3 h"
            ],
            seen);
    }

    // ---- Odd times

    // Without the picker's script she types the times; typed ones count the same.
    [Fact]
    public async Task NeverBelowZero_AndNotATime_IsAsTheServerGaveIt()
    {
        var id = await Data.AddSleepAsync(
            Users.A.BabyId, DateTime.UtcNow.AddHours(-6), TimeSpan.FromMinutes(30), null, false);
        var edge = Edge(await SleepHtmlAsync(id));
        var minute = TestData.Minute(edge.ServerNow);

        var seen = await edge.RunAsync<string[]>($$"""
            function type(id, value) {
                var inputs = document.querySelectorAll('#' + id + ' input');
                inputs[0].value = value.split(' ')[0];
                inputs[1].value = value.split(' ')[1];
                inputs[1].dispatchEvent(new Event('change', { bubbles: true }));
            }
            var seen = [];
            [['start', '{{Field(minute.AddHours(-2))}}'], ['end', '{{Field(minute.AddHours(-3))}}'], ['end', '{{Field(minute)}}'],
             ['end', '{{SleepForm.DateText(minute)}} '], ['end', ' 10:00'], ['end', '31-02-2026 10:00'], ['end', ' '],
             ['start', '{{Field(minute.AddHours(1))}}'], ['start', 'x 10:00'], ['start', ' ']].forEach(function (step) {
                type(step[0], step[1]);
                seen.push(clock());
            });
            return seen;
            """);

        Assert.Equal(
            [
                // Start after the end, end before the start, two hours.
                "0 min", "0 min", "2 h",
                // Half an end, and a day that does not exist.
                "30 min", "30 min", "30 min",
                // No end; a start in the future; no start.
                "2:00:37", "0:00:00", "30 min", "30 min"
            ],
            seen);
    }

    // The nights the clocks change in Warsaw: the same length as on the server.
    [Theory]
    [InlineData("25-10-2026")]
    [InlineData("29-03-2026")]
    public async Task ClockChangeNight_SameLengthAsTheServer(string day)
    {
        var date = DateOnly.ParseExact(day, "dd-MM-yyyy");
        DateTime? Utc(int hour, int minute) => PolandTime.TryToUtc(date, new TimeOnly(hour, minute), out var utc) ? utc : null;
        var end = Utc(6, 0)!.Value;
        var saved = end - Utc(0, 15)!.Value;
        var id = await Data.AddSleepAsync(Users.A.BabyId, end - saved, saved, null, false);
        (int Hour, int Minute)[] times = [(0, 30), (1, 0), (1, 59), (2, 0), (2, 30), (2, 59), (3, 0), (3, 30)];

        var seen = await Edge(await SleepHtmlAsync(id)).RunAsync<string[]>($$"""
            var seen = [clock()];
            for (var time of {{JsonSerializer.Serialize(times.Select(t => $"{t.Hour:00}:{t.Minute:00}"))}}) {
                await pick('start', '{{day}} ' + time);
                seen.push(clock());
            }
            return seen;
            """);

        // A skipped time is not a time: as the server gave it.
        var lengths = times.Select(t => Utc(t.Hour, t.Minute) is { } start ? end - start : saved);
        Assert.Equal(lengths.Prepend(saved).Select(Length), seen);
    }

    // The fields are Warsaw time and "now" is the server's, wherever the phone is and whatever its clock says.
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 95)]
    [InlineData(false, 95)]
    [InlineData(false, -95)]
    public async Task PhoneElsewhere_OrWithAWrongClock_SameClock(bool inTokyo, int minutesOff)
    {
        var html = await NewHtmlAsync();
        var edge = new EdgePage(Factory, html) { PhoneInTokyo = inTokyo, PhoneClockOff = TimeSpan.FromMinutes(minutesOff) };
        var minute = TestData.Minute(edge.ServerNow);

        var seen = await edge.RunAsync<string[]>($$"""
            var seen = [];
            await pick('start', '{{Field(minute.AddHours(-5))}}');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await pick('end', '{{Field(minute.AddHours(-2))}}');
            seen.push(clock());
            return seen;
            """);

        Assert.Equal(["5:00:37", "5:00:37", "3 h"], seen);
    }

    // ---- The server's answers

    // The page she gets has the times she picked; the clock agrees with them.
    [Fact]
    public async Task RefusedSave_ClockAgreesWithTheFields()
    {
        var now = TestData.Minute(DateTime.UtcNow);
        var id = await Data.AddSleepAsync(Users.A.BabyId, now.AddHours(-5), TimeSpan.FromMinutes(30), null, false);
        await Data.AddSleepAsync(Users.A.BabyId, now.AddHours(-3), TimeSpan.FromHours(1), null, false);
        using var a = await SignInAsync(Users.A.Email);

        var save = SleepForm.Fields(Users.A.BabyId, id, now.AddHours(-5), now.AddHours(-2.5));
        save["command"] = "save";
        var refusedSave = await (await a.PostAsync("/Mother/Sleep/Save", save)).Content.ReadAsStringAsync();
        Assert.Single(SleepForm.Messages(refusedSave));

        var start = SleepForm.Fields(Users.A.BabyId, null, now.AddHours(-2.5));
        start["command"] = "start";
        var refusedStart = await (await a.PostAsync("/Mother/Sleep/Save", start)).Content.ReadAsStringAsync();
        Assert.Single(SleepForm.Messages(refusedStart));

        Assert.Equal("2 h 30 min", await Edge(refusedSave).RunAsync<string>("return clock();"));
        var edge = Edge(refusedStart);
        var seen = await edge.RunAsync<string[]>("var first = clock(); await advance(1000); return [first, clock()];");
        var expected = Clock(edge.ServerNow - now.AddHours(-2.5));
        Assert.Equal([expected, expected], seen);
    }

    private static string AnswerScript(HttpStatusCode status, string json, string button) => $$"""
        answer = { status: {{(int)status}}, body: {{json}} };
        document.querySelector('[data-sleep-button]').click();
        await wait(1000);
        if (document.querySelector('[data-sleep-button]').value !== '{{button}}') { throw new Error('Not answered.'); }
        """;

    private async Task<(string Json, DateTime ServerNow)> ApiAsync(
        string command, Dictionary<string, string> fields, HttpStatusCode expected)
    {
        using var a = await SignInAsync(Users.A.Email);
        var response = await a.PostApiAsync("/api/sleep/" + command, fields);
        Assert.Equal(expected, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var screen = expected == HttpStatusCode.OK ? document.RootElement : document.RootElement.GetProperty("screen");
        return (json, DateTimeOffset.FromUnixTimeMilliseconds(screen.GetProperty("serverNow").GetInt64()).UtcDateTime);
    }

    // Picked: stands still. Start: ticks from the picked start. Stop: the length. End cleared: stands still. Resume: ticks.
    [Fact]
    public async Task StartStopAndResume_AfterAPick()
    {
        var html = await NewHtmlAsync();
        var start = TestData.Minute(DateTime.UtcNow.AddHours(-5));
        var started = await ApiAsync("start", SleepForm.Fields(Users.A.BabyId, null, start), HttpStatusCode.OK);
        using var answer = JsonDocument.Parse(started.Json);
        var id = answer.RootElement.GetProperty("sleepId").GetInt32();
        var earlier = start.AddHours(-1);
        var stopped = await ApiAsync("stop", SleepForm.Fields(Users.A.BabyId, id, earlier), HttpStatusCode.OK);
        var resumed = await ApiAsync("resume", SleepForm.Fields(Users.A.BabyId, id, earlier), HttpStatusCode.OK);

        // The server answers at once.
        var edge = new EdgePage(Factory, html) { ServerNow = started.ServerNow };
        var seen = await edge.RunAsync<string[]>($$"""
            await pick('start', '{{Field(start)}}');
            var seen = [clock()];
            await advance(2000);
            seen.push(clock());
            {{AnswerScript(HttpStatusCode.OK, started.Json, "stop")}}
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await pick('start', '{{Field(earlier)}}');
            seen.push(clock());
            {{AnswerScript(HttpStatusCode.OK, stopped.Json, "resume")}}
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            await clear('end');
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            {{AnswerScript(HttpStatusCode.OK, resumed.Json, "stop")}}
            seen.push(clock());
            await advance(1000);
            seen.push(clock());
            return seen;
            """);

        var running = started.ServerNow - start;
        var length = Length(TestData.Minute(stopped.ServerNow) - earlier);
        var sinceCleared = Clock(stopped.ServerNow - earlier + Seconds(1));
        Assert.Equal(
            [
                Clock(running), Clock(running),
                Clock(running), Clock(running + Seconds(1)),
                Clock(running + TimeSpan.FromHours(1) + Seconds(1)),
                length, length,
                sinceCleared, sinceCleared,
                Clock(resumed.ServerNow - earlier), Clock(resumed.ServerNow - earlier + Seconds(1))
            ],
            seen);
    }

    // A refused Start keeps the fields, and the clock agrees with them.
    [Fact]
    public async Task RefusedStart_AfterAPick_ClockAgreesWithTheFields()
    {
        var start = TestData.Minute(DateTime.UtcNow.AddHours(-2.5));
        await Data.AddSleepAsync(Users.A.BabyId, start.AddMinutes(-30), TimeSpan.FromHours(1), null, false);
        var html = await NewHtmlAsync();
        var refused = await ApiAsync(
            "start", SleepForm.Fields(Users.A.BabyId, null, start), HttpStatusCode.UnprocessableEntity);

        var edge = new EdgePage(Factory, html) { ServerNow = refused.ServerNow };
        var seen = await edge.RunAsync<string[]>($$"""
            await pick('start', '{{Field(start)}}');
            {{AnswerScript(HttpStatusCode.UnprocessableEntity, refused.Json, "start")}}
            var seen = [clock(), String(document.querySelectorAll('[data-sleep-messages] li').length)];
            await advance(1000);
            seen.push(clock());
            return seen;
            """);

        var expected = Clock(refused.ServerNow - start);
        Assert.Equal([expected, "1", expected], seen);
    }

    // ---- Only the clock

    private sealed record Screens(string Before, string After, string[] Clocks, string[] Sent);

    [Theory]
    [InlineData("new")]
    [InlineData("running")]
    [InlineData("stopped")]
    public async Task Pick_ChangesOnlyTheClock_AndSendsNothing(string state)
    {
        var now = TestData.Minute(DateTime.UtcNow);
        var html = state switch
        {
            "running" => await SleepHtmlAsync(await Data.AddRunningSleepAsync(Users.A.BabyId, now.AddHours(-1))),
            "stopped" => await SleepHtmlAsync(
                await Data.AddSleepAsync(Users.A.BabyId, now.AddHours(-1), TimeSpan.FromMinutes(30), null, false)),
            _ => await NewHtmlAsync()
        };
        var before = await Data.SleepSnapshotAsync();

        // The whole screen, apart from the picked field and the clock's text.
        var screens = await Edge(html).RunAsync<Screens>($$"""
            function screen() {
                var copy = document.querySelector('[data-server-now]').cloneNode(true);
                copy.querySelector('#start').remove();
                copy.querySelector('[data-sleep-clock]').textContent = '';
                var values = Array.from(new FormData(document.querySelector('[data-sleep-form]')))
                    .filter(function (pair) { return pair[0] !== 'StartDate' && pair[0] !== 'StartTime'; });
                return copy.outerHTML + JSON.stringify(values);
            }
            var before = screen();
            var clocks = [clock()];
            await pick('start', '{{Field(now.AddHours(-5))}}');
            await advance(1000);
            clocks.push(clock());
            return { before: before, after: screen(), clocks: clocks, sent: sent };
            """);

        Assert.NotEqual(screens.Clocks[0], screens.Clocks[1]);
        Assert.Contains("data-sleep-button", screens.Before);
        Assert.Equal(screens.Before, screens.After);
        Assert.Empty(screens.Sent);
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    // ---- The baby page

    [Fact]
    public async Task BabyTile_StillTicks()
    {
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-80));
        await Data.AddRunningSleepAsync(Users.A.BabyId, start);
        var edge = Edge(await HtmlAsync("/Mother/Baby"));
        var minutes = (int)(edge.ServerNow - start).TotalMinutes;

        var seen = await edge.RunAsync<string[]>("""
            var tile = document.querySelector('[data-running-since]');
            var seen = [tile.textContent];
            await advance(60000);
            seen.push(tile.textContent);
            return seen;
            """);

        Assert.Equal([Length(TimeSpan.FromMinutes(minutes)), Length(TimeSpan.FromMinutes(minutes + 1))], seen);
    }
}
