using System.Net;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// The sleep timer screen without its script: Start, Stop and Resume as normal form posts.
// SleepApiTests checks that the API does and says the same.
public sealed class SleepTimerFormTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private const string Url = "/Mother/Sleep/Save";

    public static TheoryData<string> Refusals => new(SleepForm.RefusalNames);

    private static Dictionary<string, string> With(Dictionary<string, string> fields, string command) =>
        new(fields) { ["command"] = command };

    private Task<SleepRecord> OnlySleepAsync() =>
        Data.DbAsync(db => db.SleepRecords.AsNoTracking().SingleAsync(s => s.BabyId == Users.A.BabyId));

    [Fact]
    public async Task Start_LeftAtNow_RunsFromTheMinuteOfTheTap()
    {
        using var a = await SignInAsync(Users.A.Email);
        var before = TestData.Minute(DateTime.UtcNow);

        // The page was opened 5 minutes ago and the start fields were not touched.
        var response = await a.PostAsync(Url, With(SleepForm.FieldsLeftAtNow(Users.A.BabyId, before.AddMinutes(-5)), "start"));

        var sleep = await OnlySleepAsync();
        AssertRedirect(response, $"/Mother/Sleep/Edit/{sleep.Id}");
        Assert.Null(sleep.EndTime);
        Assert.InRange(sleep.StartTime, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task Start_EditedStart_RunsFromThatTime()
    {
        using var a = await SignInAsync(Users.A.Email);
        var now = DateTime.UtcNow;
        var fields = SleepForm.Fields(Users.A.BabyId, null, now.AddMinutes(-10));
        fields["DefaultStart"] = SleepForm.DateText(now) + " " + SleepForm.TimeText(now);

        var response = await a.PostAsync(Url, With(fields, "start"));

        var sleep = await OnlySleepAsync();
        AssertRedirect(response, $"/Mother/Sleep/Edit/{sleep.Id}");
        Assert.Equal(TestData.Minute(now.AddMinutes(-10)), sleep.StartTime);
    }

    [Fact]
    public async Task Stop_SetsTheEnd_ThenResumeClearsIt()
    {
        using var a = await SignInAsync(Users.A.Email);
        var start = TestData.Minute(DateTime.UtcNow.AddMinutes(-40));
        var id = await Data.AddRunningSleepAsync(Users.A.BabyId, start);
        var before = TestData.Minute(DateTime.UtcNow);

        AssertRedirect(await a.PostAsync(Url, With(SleepForm.Fields(Users.A.BabyId, id, start), "stop")), $"/Mother/Sleep/Edit/{id}");
        var stopped = await OnlySleepAsync();
        Assert.Equal(start, stopped.StartTime);
        Assert.InRange(stopped.EndTime!.Value, before, DateTime.UtcNow);
        Assert.Contains("value=\"resume\"", await a.GetHtmlAsync($"/Mother/Sleep/Edit/{id}"));

        var fields = SleepForm.Fields(Users.A.BabyId, id, start, stopped.EndTime);
        AssertRedirect(await a.PostAsync(Url, With(fields, "resume")), $"/Mother/Sleep/Edit/{id}");
        var resumed = await OnlySleepAsync();
        Assert.Equal(start, resumed.StartTime);
        Assert.Null(resumed.EndTime);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Refused_ShowsItsMessage_NothingSaved(string name)
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, name);
        var before = await Data.SleepSnapshotAsync();

        var response = await a.PostAsync(Url, With(refusal.Fields, refusal.Command));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(SleepForm.Messages(await response.Content.ReadAsStringAsync()));
        Assert.StartsWith(refusal.Message, message);
        Assert.Equal(before, await Data.SleepSnapshotAsync());
    }

    // The screen comes from the database, what she typed stays: the refused Resume is gone, her note is still there.
    [Fact]
    public async Task RefusedResume_HasNoBigButton_KeepsHerNote()
    {
        using var a = await SignInAsync(Users.A.Email);
        var refusal = await SleepForm.ArrangeAsync(Data, Users.A.BabyId, "resume after an hour");
        var note = TestData.Marker("SleepNote");
        refusal.Fields["Note"] = note;

        var html = await (await a.PostAsync(Url, With(refusal.Fields, refusal.Command))).Content.ReadAsStringAsync();

        Assert.DoesNotContain("value=\"resume\"", html);
        Assert.Contains(note, html);
    }
}
