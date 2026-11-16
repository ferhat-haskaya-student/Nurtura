using System.Net;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// Mother B must never open or change mother A's records by putting A's ids (or A's dates) in a URL or a form.
// Each refused case has the allowed case for A next to it.
public sealed class MotherIsolationTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private sealed record ARecords(int Baby, int Sleep, int Feed, int Upcoming, int Past, string SleepNote, string FeedNote);

    private async Task<ARecords> SeedARecordsAsync()
    {
        var a = Users.A;
        var sleepNote = TestData.Marker("SleepNoteA");
        var feedNote = TestData.Marker("FeedNoteA");
        var sleep = await Data.AddSleepAsync(a.BabyId, DateTime.UtcNow.AddHours(-6), TimeSpan.FromHours(1), sleepNote, false);
        var feed = await Data.AddFeedAsync(a.BabyId, DateTime.UtcNow.AddHours(-3), feedNote, false);
        var upcoming = await Data.AddAppointmentAsync(a.ProfileId, Users.P1.ProfileId, TestData.UpcomingDate(), AppointmentStatus.Confirmed);
        var past = await Data.AddAppointmentAsync(a.ProfileId, Users.P1.ProfileId, TestData.PastDate(), AppointmentStatus.Completed);
        return new ARecords(a.BabyId, sleep, feed, upcoming, past, sleepNote, feedNote);
    }

    // Every mother page that takes the id of a record.
    public static TheoryData<string> RecordPages => new()
    {
        "/Mother/Baby/Index/{baby}",
        "/Mother/Baby/Edit/{baby}",
        "/Mother/Baby/Routine/{baby}",
        "/Mother/Baby/Remove/{baby}",
        "/Mother/Sleep/Timer/{baby}",
        "/Mother/Sleep/Edit/{sleep}",
        "/Mother/Feeding/Log/{baby}",
        "/Mother/Feeding/Edit/{feed}",
        "/Mother/Appointments/Chat/{upcoming}",
        "/Mother/Appointments/Consultation/{past}"
    };

    private static string Fill(string template, ARecords r) => template
        .Replace("{baby}", r.Baby.ToString())
        .Replace("{sleep}", r.Sleep.ToString())
        .Replace("{feed}", r.Feed.ToString())
        .Replace("{upcoming}", r.Upcoming.ToString())
        .Replace("{past}", r.Past.ToString());

    // ---- Check-ins (addressed by date, not id: the date alone must never reach another mother's check-in)

    [Fact]
    public async Task CheckIn_Owner_SeesHerNoteForTheDate()
    {
        var date = PolandTime.Today.AddDays(-1);
        var note = TestData.Marker("CheckInNoteA");
        await Data.AddCheckInAsync(Users.A.ProfileId, date, note, shared: false);

        using var a = await SignInAsync(Users.A.Email);
        Assert.Contains(note, await a.GetHtmlAsync($"/Mother/CheckIn?date={Date(date)}"));
    }

    [Fact]
    public async Task CheckIn_OtherMotherSameDate_SeesNothingOfAs()
    {
        var date = PolandTime.Today.AddDays(-1);
        var note = TestData.Marker("CheckInNoteA");
        await Data.AddCheckInAsync(Users.A.ProfileId, date, note, shared: false);

        using var b = await SignInAsync(Users.B.Email);
        Assert.DoesNotContain(note, await b.GetHtmlAsync($"/Mother/CheckIn?date={Date(date)}"));
        Assert.DoesNotContain(note, await b.GetHtmlAsync($"/Mother/CheckIn/Week?date={Date(date)}"));
        Assert.DoesNotContain(note, await b.GetHtmlAsync("/Mother"));
    }

    [Fact]
    public async Task CheckIn_OtherMotherSavesAndSharesSameDate_AsRowUnchanged()
    {
        var date = PolandTime.Today.AddDays(-1);
        var noteA = TestData.Marker("CheckInNoteA");
        var idA = await Data.AddCheckInAsync(Users.A.ProfileId, date, noteA, shared: false);

        using var b = await SignInAsync(Users.B.Email);
        var noteB = TestData.Marker("CheckInNoteB");
        AssertRedirect(await b.PostAsync("/Mother/CheckIn", new()
        {
            ["Date"] = Date(date), ["Mood"] = "5", ["SleepQuality"] = "5", ["Energy"] = "5", ["Stress"] = "1",
            ["Note"] = noteB, ["IsShared"] = "true"
        }), "/Mother");
        AssertRedirect(await b.PostAsync("/Mother/CheckIn/Share", new()
        {
            ["date"] = Date(date), ["isShared"] = "true"
        }), "/Mother/CheckIn/Day");

        var rows = await Data.DbAsync(db => db.DailyCheckIns.AsNoTracking().Where(c => c.Date == date).ToListAsync());
        var rowA = Assert.Single(rows, c => c.MotherProfileId == Users.A.ProfileId);
        Assert.Equal(idA, rowA.Id);
        Assert.Equal(noteA, rowA.Note);
        Assert.Equal(2, rowA.Mood);
        Assert.False(rowA.IsShared);

        // B's save went to her own row.
        Assert.Equal(noteB, Assert.Single(rows, c => c.MotherProfileId == Users.B.ProfileId).Note);
    }

    // ---- Records reached by id

    [Theory]
    [MemberData(nameof(RecordPages))]
    public async Task RecordPage_Owner_Opens(string page)
    {
        var records = await SeedARecordsAsync();
        using var a = await SignInAsync(Users.A.Email);

        var response = await a.GetAsync(Fill(page, records));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RecordPages))]
    public async Task RecordPage_OtherMotherById_Gets404(string page)
    {
        var records = await SeedARecordsAsync();
        using var b = await SignInAsync(Users.B.Email);

        var response = await b.GetAsync(Fill(page, records));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string> RecordChanges => new() { "baby edit", "baby remove", "sleep save", "sleep reset", "feed save", "feed delete" };

    private static (string Url, Dictionary<string, string> Form) Change(string change, ARecords r)
    {
        var yesterday = Date(PolandTime.Today.AddDays(-1));
        return change switch
        {
            "baby edit" => ($"/Mother/Baby/Edit/{r.Baby}", new()
            {
                ["Name"] = "Changed by B", ["BirthDate"] = yesterday, ["BirthType"] = "Natural", ["BirthWeek"] = "40",
                ["FeedingType"] = "Bottle"
            }),
            "baby remove" => ($"/Mother/Baby/Remove/{r.Baby}", new()),
            "sleep save" => ("/Mother/Sleep/Save", new()
            {
                ["babyId"] = r.Baby.ToString(), ["sleepId"] = r.Sleep.ToString(), ["command"] = "save",
                ["StartDate"] = yesterday, ["StartTime"] = "01:00", ["EndDate"] = yesterday, ["EndTime"] = "02:00",
                ["Note"] = "Changed by B", ["IsShared"] = "true"
            }),
            "sleep reset" => ($"/Mother/Sleep/Reset/{r.Sleep}", new()),
            "feed save" => ("/Mother/Feeding/Save", new()
            {
                ["babyId"] = r.Baby.ToString(), ["feedId"] = r.Feed.ToString(), ["Date"] = yesterday, ["Time"] = "03:00",
                ["Type"] = "Breast", ["BreastSide"] = "Right", ["Note"] = "Changed by B", ["IsShared"] = "true"
            }),
            "feed delete" => ($"/Mother/Feeding/Delete/{r.Feed}", new()),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
    }

    // Everything of A's that the changes above could touch, as one comparable string.
    private Task<string> SnapshotAAsync() =>
        Data.DbAsync(async db =>
        {
            var babies = await db.Babies.AsNoTracking().Where(b => b.MotherProfileId == Users.A.ProfileId)
                .OrderBy(b => b.Id).Select(b => $"{b.Id}|{b.Name}|{b.BirthDate}|{b.FeedingType}").ToListAsync();
            var sleeps = await db.SleepRecords.AsNoTracking().Where(s => s.BabyId == Users.A.BabyId)
                .OrderBy(s => s.Id).Select(s => $"{s.Id}|{s.StartTime:O}|{s.EndTime:O}|{s.Note}|{s.IsShared}").ToListAsync();
            var feeds = await db.FeedingRecords.AsNoTracking().Where(f => f.BabyId == Users.A.BabyId)
                .OrderBy(f => f.Id).Select(f => $"{f.Id}|{f.Time:O}|{f.BreastSide}|{f.Note}|{f.IsShared}").ToListAsync();
            return string.Join("\n", babies.Concat(sleeps).Concat(feeds));
        });

    [Theory]
    [MemberData(nameof(RecordChanges))]
    public async Task RecordChange_OtherMotherById_Gets404AndNothingChanges(string change)
    {
        var records = await SeedARecordsAsync();
        var before = await SnapshotAAsync();
        using var b = await SignInAsync(Users.B.Email);

        var (url, form) = Change(change, records);
        var response = await b.PostAsync(url, form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await SnapshotAAsync());
    }

    [Fact]
    public async Task RecordChange_Owner_Saves()
    {
        var records = await SeedARecordsAsync();
        using var a = await SignInAsync(Users.A.Email);

        var (url, form) = Change("baby edit", records);
        AssertRedirect(await a.PostAsync(url, form), "/Mother/Baby");

        var name = await Data.DbAsync(db => db.Babies.Where(b => b.Id == records.Baby).Select(b => b.Name).SingleAsync());
        Assert.Equal("Changed by B", name);
    }

    // ---- Appointments: cancel and the "What would you like to talk about?" note

    private Task<(AppointmentStatus Status, string Concern)> AppointmentAsync(int id) =>
        Data.DbAsync(db => db.Appointments.Where(x => x.Id == id)
            .Select(x => new ValueTuple<AppointmentStatus, string>(x.Status, x.ConcernText)).SingleAsync());

    [Fact]
    public async Task Appointment_OtherMotherCancelOrEditNote_LeavesItUnchanged()
    {
        var records = await SeedARecordsAsync();
        var before = await AppointmentAsync(records.Upcoming);
        using var b = await SignInAsync(Users.B.Email);

        AssertRedirect(await b.PostAsync($"/Mother/Appointments/Cancel/{records.Upcoming}", new()), "/Mother/Appointments");
        AssertRedirect(await b.GetAsync($"/Mother/Appointments/EditNote/{records.Upcoming}"), "/Mother/Appointments");
        AssertRedirect(await b.PostAsync("/Mother/Appointments/EditNote", new()
        {
            ["Id"] = records.Upcoming.ToString(), ["Concern"] = "Changed by B"
        }), "/Mother/Appointments");

        Assert.Equal(before, await AppointmentAsync(records.Upcoming));
        Assert.Equal(AppointmentStatus.Confirmed, before.Status);
    }

    [Fact]
    public async Task Appointment_OwnerEditsNote_Saved()
    {
        var records = await SeedARecordsAsync();
        using var a = await SignInAsync(Users.A.Email);

        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/Mother/Appointments/EditNote/{records.Upcoming}")).StatusCode);
        AssertRedirect(await a.PostAsync("/Mother/Appointments/EditNote", new()
        {
            ["Id"] = records.Upcoming.ToString(), ["Concern"] = "Changed by A"
        }), "/Mother/Appointments");

        Assert.Equal("Changed by A", (await AppointmentAsync(records.Upcoming)).Concern);
    }

    // ---- Notifications

    private Task<bool> IsReadAsync(int id) =>
        Data.DbAsync(db => db.Notifications.Where(n => n.Id == id).Select(n => n.IsRead).SingleAsync());

    [Fact]
    public async Task Notification_OtherMotherOpensById_Gets404AndStaysUnread()
    {
        var records = await SeedARecordsAsync();
        var id = await Data.AddNotificationAsync(Users.A.UserId, NotificationType.RequestAccepted, Users.P1.DisplayName, records.Upcoming);
        using var b = await SignInAsync(Users.B.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/Notifications/Open/{id}")).StatusCode);
        Assert.DoesNotContain(Users.P1.DisplayName, await b.GetHtmlAsync("/Notifications"));
        Assert.False(await IsReadAsync(id));
    }

    [Fact]
    public async Task Notification_OwnerOpens_RedirectsAndMarksRead()
    {
        var records = await SeedARecordsAsync();
        var id = await Data.AddNotificationAsync(Users.A.UserId, NotificationType.RequestAccepted, Users.P1.DisplayName, records.Upcoming);
        using var a = await SignInAsync(Users.A.Email);

        Assert.Contains(Users.P1.DisplayName, await a.GetHtmlAsync("/Notifications"));
        AssertRedirect(await a.GetAsync($"/Notifications/Open/{id}"), "/Mother/Appointments");
        Assert.True(await IsReadAsync(id));
    }
}
