using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// The consultation chat (SignalR hub): only the mother and the professional of that appointment get in,
// and a deactivated professional gets into none.
public sealed class ChatHubAccessTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private static async Task<JsonElement> JoinAsync(AppClient client, int appointmentId)
    {
        await using var hub = client.Hub();
        await hub.StartAsync();
        return await hub.InvokeAsync<JsonElement>("JoinRoom", appointmentId);
    }

    private static async Task AssertRefusedAsync(AppClient client, int appointmentId)
    {
        var error = await Assert.ThrowsAsync<HubException>(() => JoinAsync(client, appointmentId));
        Assert.Contains("NotFound", error.Message);
    }

    // A booked P2: one upcoming confirmed appointment, one past completed one with a message in it.
    private async Task<(int Upcoming, int Past)> SeedP2AppointmentsAsync()
    {
        var upcoming = await Data.AddAppointmentAsync(Users.A.ProfileId, Users.P2.ProfileId, TestData.UpcomingDate(), AppointmentStatus.Confirmed);
        var past = await Data.AddAppointmentAsync(Users.A.ProfileId, Users.P2.ProfileId, TestData.PastDate(), AppointmentStatus.Completed);
        await Data.AddChatMessageAsync(past, AppointmentParty.Mother, TestData.Marker("Chat"));
        return (upcoming, past);
    }

    [Fact]
    public async Task JoinRoom_OwnAppointment_Succeeds()
    {
        var (upcoming, past) = await SeedP2AppointmentsAsync();
        using var p2 = await SignInAsync(Users.P2.Email);

        Assert.Equal("Professional", (await JoinAsync(p2, upcoming)).GetProperty("me").GetString());
        Assert.Equal(1, (await JoinAsync(p2, past)).GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task JoinRoom_OtherProfessionalsAppointment_Refused()
    {
        var (upcoming, past) = await SeedP2AppointmentsAsync();
        using var p1 = await SignInAsync(Users.P1.Email);

        await AssertRefusedAsync(p1, upcoming);
        await AssertRefusedAsync(p1, past);
    }

    [Fact]
    public async Task JoinRoom_DeactivatedProfessional_Refused()
    {
        var (upcoming, past) = await SeedP2AppointmentsAsync();
        using var p2 = await SignInAsync(Users.P2.Email);
        await JoinAsync(p2, upcoming);

        // Set directly: the admin's Deactivate would also cancel the upcoming appointment, which would hide
        // whether the refusal comes from the deactivation itself.
        await Data.DbAsync(db => db.ProfessionalProfiles.Where(p => p.Id == Users.P2.ProfileId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false)));

        await AssertRefusedAsync(p2, upcoming);
        await AssertRefusedAsync(p2, past);
    }
}
