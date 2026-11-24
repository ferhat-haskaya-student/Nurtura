using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// The sleep timer API (/api/sleep). AnonymousAccessTests finds only classes that derive from Controller, so who may
// call the API is tested here: every refused case, the allowed ones next to them, and the same messages as the form.
public sealed class SleepApiTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    public static TheoryData<string> Commands => new("start", "stop", "resume");

    public static TheoryData<string> Refusals => new(SleepForm.RefusalNames);

    private static string Url(string command) => "/api/sleep/" + command;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode status, string contentType)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static Task<JsonElement> SavedAsync(HttpResponseMessage response) =>
        JsonAsync(response, HttpStatusCode.OK, "application/json");

    private static Task<JsonElement> RefusedAsync(HttpResponseMessage response) =>
        JsonAsync(response, HttpStatusCode.UnprocessableEntity, "application/problem+json");

    private static string? Text(JsonElement json, string name) => json.GetProperty(name).GetString();

    private static bool IsNull(JsonElement json, string name) => json.GetProperty(name).ValueKind == JsonValueKind.Null;

    private static long Milliseconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();

    private Task<SleepRecord> OnlySleepAsync() =>
        Data.DbAsync(db => db.SleepRecords.AsNoTracking().SingleAsync());

    // A sleep of mother A that the command would be allowed on (start: none, on her baby).
    private async Task<Dictionary<string, string>> FieldsForAAsync(string command)
    {
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-40));
        switch (command)
        {
            case "stop":
                return SleepForm.Fields(Users.A.BabyId, await Data.AddRunningSleepAsync(Users.A.BabyId, start), start);
            case "resume":
                var id = await Data.AddSleepAsync(Users.A.BabyId, start, TimeSpan.FromMinutes(30), null, false);
                return SleepForm.Fields(Users.A.BabyId, id, start, start.AddMinutes(30));
            default:
                return SleepForm.Fields(Users.A.BabyId, null, start);
        }
    }

    // ---- Who may call it

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task NotSignedIn_401_NothingSaved(string command)
    {
        var fields = await FieldsForAAsync(command);
        var before = await Data.SleepSnapshotAsync();
        using var client = Anonymous();

        var response = await client.PostApiAsync(Url(command), fields);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Professional_403_NothingSaved(string command)
    {
        var fields = await FieldsForAAsync(command);
        var before = await Data.SleepSnapshotAsync();
        using var professional = await SignInAsync(Users.P1.Email);

        var response = await professional.PostApiAsync(Url(command), fields);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task OtherMother_404_NothingSaved(string command)
    {
        var fields = await FieldsForAAsync(command);
        var before = await Data.SleepSnapshotAsync();
        using var b = await SignInAsync(Users.B.Email);

        var response = await b.PostApiAsync(Url(command), fields);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    // Her own baby's id next to A's sleep id does not help, and a wrong field does not change the answer.
    [Theory]
    [InlineData("stop")]
    [InlineData("resume")]
    public async Task OtherMother_OwnBabyWithAsSleep_404_NothingSaved(string command)
    {
        var fields = await FieldsForAAsync(command);
        fields["babyId"] = Users.B.BabyId.ToString();
        fields["StartDate"] = "wrong";
        var before = await Data.SleepSnapshotAsync();
        using var b = await SignInAsync(Users.B.Email);

        var response = await b.PostApiAsync(Url(command), fields);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task WithoutToken_400_NothingSaved(string command)
    {
        var fields = await FieldsForAAsync(command);
        var before = await Data.SleepSnapshotAsync();
        using var a = await SignInAsync(Users.A.Email);

        var response = await a.PostApiAsync(Url(command), fields, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await Data.SleepSnapshotAsync());

        // The same call with the token is allowed.
        Assert.Equal(HttpStatusCode.OK, (await a.PostApiAsync(Url(command), fields)).StatusCode);
        Assert.NotEqual(before, await Data.SleepSnapshotAsync());
    }

    [Fact]
    public async Task WrongToken_400_NothingSaved()
    {
        var fields = await FieldsForAAsync("start");
        fields["__RequestVerificationToken"] = "wrong";
        using var a = await SignInAsync(Users.A.Email);

        var response = await a.PostApiAsync(Url("start"), fields, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("", await Data.SleepSnapshotAsync());
    }

    // The onboarding filter runs for the API like for every Mother page.
    [Fact]
    public async Task OnboardingNotFinished_SentToOnboarding_NothingSaved()
    {
        using var a = await SignInAsync(Users.A.Email);
        var fields = await FieldsForAAsync("start");
        await Data.DbAsync(db => db.MotherProfiles.Where(p => p.Id == Users.A.ProfileId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.OnboardingCompletedAt, (DateTime?)null)));

        AssertRedirect(await a.PostApiAsync(Url("start"), fields, tokenPage: "/Mother/Onboarding"), "/Mother/Onboarding");

        Assert.Equal("", await Data.SleepSnapshotAsync());
    }

    [Fact]
    public async Task OnlyPost_AndOnlyTheThreeCommands()
    {
        using var a = await SignInAsync(Users.A.Email);
        var fields = await FieldsForAAsync("start");

        Assert.False((await a.GetAsync(Url("start"))).IsSuccessStatusCode);
        Assert.False((await a.PostApiAsync(Url("save"), fields)).IsSuccessStatusCode);
        Assert.Equal("", await Data.SleepSnapshotAsync());
    }

    // ---- What it does

    [Fact]
    public async Task StartThenStop_OneSleepWithTheRightTimes()
    {
        using var a = await SignInAsync(Users.A.Email);
        var now = DateTime.UtcNow;
        var start = TestData.Minute(now.AddMinutes(-10));
        var note = TestData.Marker("SleepNote");

        // "Fell asleep 10 minutes ago", with a note, shared.
        var fields = SleepForm.Fields(Users.A.BabyId, null, start);
        fields["DefaultStart"] = SleepForm.DateText(now) + " " + SleepForm.TimeText(now);
        fields["Note"] = " " + note + "\r\nsecond line ";
        fields["IsShared"] = "true";
        var running = await SavedAsync(await a.PostApiAsync(Url("start"), fields));

        var sleep = await OnlySleepAsync();
        Assert.Equal(Users.A.BabyId, sleep.BabyId);
        Assert.Equal(start, sleep.StartTime);
        Assert.Null(sleep.EndTime);
        Assert.Equal(sleep.Id, running.GetProperty("sleepId").GetInt32());
        Assert.Equal("stop", Text(running, "command"));
        Assert.Equal("Stop", Text(running, "commandText"));
        Assert.Equal(Milliseconds(start), running.GetProperty("runningSince").GetInt64());
        Assert.True(IsNull(running, "durationText"));
        Assert.Equal(SleepForm.DateText(start), Text(running, "startDate"));
        Assert.Equal(SleepForm.TimeText(start), Text(running, "startTime"));
        Assert.True(IsNull(running, "endDate"));
        Assert.True(IsNull(running, "endTime"));
        Assert.Equal(note + "\nsecond line", Text(running, "note"));
        Assert.True(running.GetProperty("isShared").GetBoolean());
        Assert.False(running.GetProperty("isLongRunning").GetBoolean());
        Assert.Equal($"/Mother/Sleep/Edit/{sleep.Id}", Text(running, "editUrl"));
        Assert.Equal($"/Mother/Sleep/Reset/{sleep.Id}", Text(running, "resetUrl"));

        fields["sleepId"] = sleep.Id.ToString();
        var before = TestData.Minute(DateTime.UtcNow);
        var stopped = await SavedAsync(await a.PostApiAsync(Url("stop"), fields));

        var ended = await OnlySleepAsync();
        Assert.Equal(sleep.Id, ended.Id);
        Assert.Equal(start, ended.StartTime);
        Assert.InRange(ended.EndTime!.Value, before, DateTime.UtcNow);
        Assert.Equal("resume", Text(stopped, "command"));
        Assert.Equal("Resume", Text(stopped, "commandText"));
        Assert.True(IsNull(stopped, "runningSince"));
        Assert.Equal($"{(ended.EndTime.Value - start).TotalMinutes} min", Text(stopped, "durationText"));
        Assert.Equal(SleepForm.DateText(ended.EndTime.Value), Text(stopped, "endDate"));
        Assert.Equal(SleepForm.TimeText(ended.EndTime.Value), Text(stopped, "endTime"));
    }

    // Start fields left at the "now" of a page opened 5 minutes ago: the minute of the tap counts.
    [Fact]
    public async Task Start_LeftAtNow_RunsFromTheMinuteOfTheTap()
    {
        using var a = await SignInAsync(Users.A.Email);
        var before = TestData.Minute(DateTime.UtcNow);

        await SavedAsync(await a.PostApiAsync(Url("start"), SleepForm.FieldsLeftAtNow(Users.A.BabyId, before.AddMinutes(-5))));

        Assert.InRange((await OnlySleepAsync()).StartTime, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task Resume_RunsAgainFromTheOriginalStart()
    {
        using var a = await SignInAsync(Users.A.Email);
        var fields = await FieldsForAAsync("resume");
        var start = (await OnlySleepAsync()).StartTime;

        var running = await SavedAsync(await a.PostApiAsync(Url("resume"), fields));

        var sleep = await OnlySleepAsync();
        Assert.Equal(start, sleep.StartTime);
        Assert.Null(sleep.EndTime);
        Assert.Equal("stop", Text(running, "command"));
        Assert.Equal(Milliseconds(start), running.GetProperty("runningSince").GetInt64());
        Assert.True(IsNull(running, "endDate"));
    }

    [Fact]
    public async Task Start_13HoursAgo_LongRunning()
    {
        using var a = await SignInAsync(Users.A.Email);

        var fields = SleepForm.Fields(Users.A.BabyId, null, DateTime.UtcNow.AddHours(-13));
        var running = await SavedAsync(await a.PostApiAsync(Url("start"), fields));

        Assert.True(running.GetProperty("isLongRunning").GetBoolean());
    }

    // ---- Refused by a rule: the form's message, and the state as it is

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Refused_SameMessageAsTheForm_NothingSaved(string name)
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, name);
        var before = await Data.SleepSnapshotAsync();

        var form = await a.PostAsync("/Mother/Sleep/Save", new(refusal.Fields) { ["command"] = refusal.Command });
        var formMessages = SleepForm.Messages(await form.Content.ReadAsStringAsync());
        var refused = await RefusedAsync(await a.PostApiAsync(Url(refusal.Command), refusal.Fields));

        var messages = refused.GetProperty("errors").EnumerateObject()
            .SelectMany(e => e.Value.EnumerateArray().Select(m => m.GetString()!)).ToList();
        Assert.Equal(formMessages, messages);
        Assert.StartsWith(refusal.Message, Assert.Single(messages));
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    [Fact]
    public async Task Refused_Polish_PolishMessageAndTexts()
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, "resume after an hour");

        var refused = await RefusedAsync(await a.PostApiAsync(Url("resume"), refusal.Fields, language: "pl"));
        Assert.Equal("Ten sen skończył się zbyt dawno, aby go wznowić. Rozpocznij nowy sen.",
            refused.GetProperty("errors").GetProperty("")[0].GetString());

        var running = await SavedAsync(
            await a.PostApiAsync(Url("start"), SleepForm.Fields(Users.A.BabyId, null, DateTime.UtcNow), language: "pl"));
        Assert.Equal("Stop", Text(running, "commandText"));
        var fields = SleepForm.Fields(Users.A.BabyId, running.GetProperty("sleepId").GetInt32(), DateTime.UtcNow.AddMinutes(-9));
        Assert.Equal("Wznów", Text(await SavedAsync(await a.PostApiAsync(Url("stop"), fields, language: "pl")), "commandText"));
    }

    [Fact]
    public async Task RefusedResume_HasNoBigButton()
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, "resume after an hour");

        var screen = (await RefusedAsync(await a.PostApiAsync(Url("resume"), refusal.Fields))).GetProperty("screen");

        Assert.True(IsNull(screen, "command"));
        Assert.True(IsNull(screen, "commandText"));
        Assert.Equal("30 min", Text(screen, "durationText"));
        Assert.Equal(refusal.Fields["sleepId"], screen.GetProperty("sleepId").GetInt32().ToString());
    }

    [Fact]
    public async Task RefusedStop_ScreenStillRuns()
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, "too short");

        var screen = (await RefusedAsync(await a.PostApiAsync(Url("stop"), refusal.Fields))).GetProperty("screen");

        Assert.Equal("stop", Text(screen, "command"));
        Assert.Equal(Milliseconds((await OnlySleepAsync()).StartTime), screen.GetProperty("runningSince").GetInt64());
        Assert.True(IsNull(screen, "endDate"));
    }

    [Fact]
    public async Task RefusedStart_ScreenNotStarted()
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, "already running");

        var screen = (await RefusedAsync(await a.PostApiAsync(Url("start"), refusal.Fields))).GetProperty("screen");

        Assert.Equal("start", Text(screen, "command"));
        Assert.True(IsNull(screen, "sleepId"));
        Assert.True(IsNull(screen, "runningSince"));
        Assert.True(IsNull(screen, "durationText"));
        Assert.True(IsNull(screen, "editUrl"));
        Assert.True(IsNull(screen, "resetUrl"));
    }
}
