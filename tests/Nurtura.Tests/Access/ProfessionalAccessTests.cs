using System.Net;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// The pre-consultation summary: only the professional of the appointment, only what the mother marked shared,
// only until the session ends. And a professional never gets into the Mother area.
public sealed class ProfessionalAccessTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private sealed record Seeded(int CheckInAppointment, int SleepAppointment, string[] Shared, string[] Private);

    // A has shared and private check-ins and sleeps, and two upcoming appointments with P1:
    // one on a check-in topic and one on baby sleep.
    private async Task<Seeded> SeedAsync()
    {
        var a = Users.A;
        var sharedCheckIn = TestData.Marker("SharedCheckIn");
        var privateCheckIn = TestData.Marker("PrivateCheckIn");
        var sharedSleep = TestData.Marker("SharedSleep");
        var privateSleep = TestData.Marker("PrivateSleep");

        await Data.AddCheckInAsync(a.ProfileId, PolandTime.Today.AddDays(-1), sharedCheckIn, shared: true);
        await Data.AddCheckInAsync(a.ProfileId, PolandTime.Today.AddDays(-2), privateCheckIn, shared: false);
        await Data.AddSleepAsync(a.BabyId, DateTime.UtcNow.AddDays(-1), TimeSpan.FromHours(1), sharedSleep, shared: true);
        await Data.AddSleepAsync(a.BabyId, DateTime.UtcNow.AddDays(-2), TimeSpan.FromHours(1), privateSleep, shared: false);

        var date = TestData.UpcomingDate();
        var checkInAppointment = await Data.AddAppointmentAsync(
            a.ProfileId, Users.P1.ProfileId, date, AppointmentStatus.Confirmed, Topic.PostpartumWellbeing, hour: 10);
        var sleepAppointment = await Data.AddAppointmentAsync(
            a.ProfileId, Users.P1.ProfileId, date, AppointmentStatus.Requested, Topic.BabySleep, babyId: a.BabyId, hour: 11);
        return new Seeded(checkInAppointment, sleepAppointment, [sharedCheckIn, sharedSleep], [privateCheckIn, privateSleep]);
    }

    [Fact]
    public async Task Summary_OwnAppointment_ShowsSharedRecordsOnly()
    {
        var seeded = await SeedAsync();
        using var p1 = await SignInAsync(Users.P1.Email);

        var html = await p1.GetHtmlAsync($"/Professional/Appointments/Summary/{seeded.CheckInAppointment}")
                   + await p1.GetHtmlAsync($"/Professional/Appointments/Summary/{seeded.SleepAppointment}");

        Assert.Contains(Users.A.DisplayName, html);
        Assert.All(seeded.Shared, text => Assert.Contains(text, html));
        Assert.All(seeded.Private, text => Assert.DoesNotContain(text, html));
    }

    [Fact]
    public async Task Summary_OtherProfessional_Gets404()
    {
        var seeded = await SeedAsync();
        using var p2 = await SignInAsync(Users.P2.Email);

        foreach (var id in new[] { seeded.CheckInAppointment, seeded.SleepAppointment })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await p2.GetAsync($"/Professional/Appointments/Summary/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await p2.GetAsync($"/Professional/Appointments/Chat/{id}")).StatusCode);
        }
    }

    [Theory]
    [InlineData("hour passed")]
    [InlineData("ended early")]
    public async Task Summary_AfterSessionEnded_Gets404(string ending)
    {
        await SeedAsync();
        var appointment = ending == "hour passed"
            ? await Data.AddAppointmentAsync(Users.A.ProfileId, Users.P1.ProfileId, TestData.PastDate(), AppointmentStatus.Confirmed)
            : await Data.AddAppointmentAsync(Users.A.ProfileId, Users.P1.ProfileId, TestData.UpcomingDate(),
                AppointmentStatus.Confirmed, hour: 12, sessionEndedAtUtc: DateTime.UtcNow.AddMinutes(-1));
        using var p1 = await SignInAsync(Users.P1.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await p1.GetAsync($"/Professional/Appointments/Summary/{appointment}")).StatusCode);
    }

    [Theory]
    [InlineData("/Mother")]
    [InlineData("/Mother/CheckIn")]
    [InlineData("/Mother/Baby")]
    [InlineData("/Mother/Appointments")]
    [InlineData("/Mother/Forum")]
    [InlineData("/Mother/MyAccount")]
    public async Task MotherArea_Professional_AccessDenied(string page)
    {
        using var p1 = await SignInAsync(Users.P1.Email);

        AssertRedirect(await p1.GetAsync(page), "/Account/AccessDenied");
    }

    [Theory]
    [InlineData("/Professional")]
    [InlineData("/Professional/Appointments")]
    [InlineData("/Professional/Availability")]
    public async Task OwnArea_Professional_Opens(string page)
    {
        using var p1 = await SignInAsync(Users.P1.Email);

        Assert.Equal(HttpStatusCode.OK, (await p1.GetAsync(page)).StatusCode);
    }
}
