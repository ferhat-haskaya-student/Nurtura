using System.Net;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// The admin runs accounts, content and the forum. She never gets into the Mother or Professional areas, and her
// pages never show check-in notes, chats, closing notes or who reported something.
public sealed class AdminAccessTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("/Mother")]
    [InlineData("/Mother/CheckIn")]
    [InlineData("/Mother/Baby")]
    [InlineData("/Mother/Forum")]
    [InlineData("/Professional")]
    [InlineData("/Professional/Appointments")]
    [InlineData("/Notifications")]
    public async Task MotherAndProfessionalAreas_Admin_AccessDenied(string page)
    {
        using var admin = await SignInAsync(Users.AdminEmail);

        AssertRedirect(await admin.GetAsync(page), "/Account/AccessDenied");
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Admin/Professionals")]
    [InlineData("/Admin/Guides")]
    public async Task AdminArea_Admin_Opens(string page)
    {
        using var admin = await SignInAsync(Users.AdminEmail);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(page)).StatusCode);
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Admin/Reports/Hidden")]
    public async Task AdminPages_ContainNoPrivateText(string page)
    {
        var a = Users.A;

        // Private text that must never reach the admin.
        var checkInNote = TestData.Marker("CheckInNote");
        var chatText = TestData.Marker("ChatText");
        var closingNote = TestData.Marker("ClosingNote");
        await Data.AddCheckInAsync(a.ProfileId, PolandTime.Today, checkInNote, shared: true);
        var past = await Data.AddAppointmentAsync(a.ProfileId, Users.P1.ProfileId, TestData.PastDate(), AppointmentStatus.Completed);
        await Data.AddChatMessageAsync(past, AppointmentParty.Mother, chatText);
        await Data.AddChatMessageAsync(past, AppointmentParty.Professional, chatText + "Reply");
        await Data.AddClosingNoteAsync(past, closingNote);

        // Forum items by B, reported by A: one open report (Reports) and one hidden comment (Hidden list).
        var reportedBody = TestData.Marker("ReportedBody");
        var hiddenBody = TestData.Marker("HiddenComment");
        var post = await Data.AddPostAsync(Users.B.ProfileId, TestData.Marker("ReportedTitle"), reportedBody);
        var comment = await Data.AddCommentAsync(post, Users.B.ProfileId, hiddenBody);
        await Data.AddPostReportAsync(a.ProfileId, post, "Please look at this.");
        await Data.AddCommentReportAsync(a.ProfileId, comment, "This one too.");
        await Data.DbAsync(db => db.ForumComments.Where(c => c.Id == comment)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.HiddenByModeratorAt, DateTime.UtcNow)));

        using var admin = await SignInAsync(Users.AdminEmail);
        var html = await admin.GetHtmlAsync(page);

        Assert.DoesNotContain(checkInNote, html);
        Assert.DoesNotContain(chatText, html);
        Assert.DoesNotContain(closingNote, html);
        Assert.DoesNotContain(a.DisplayName, html);
        Assert.DoesNotContain(a.Email, html);

        // The allowed side: the page really shows the moderation content it is for.
        if (page == "/Admin/Reports")
        {
            Assert.Contains(reportedBody, html);
        }
        else if (page == "/Admin/Reports/Hidden")
        {
            Assert.Contains(hiddenBody, html);
        }
    }
}
