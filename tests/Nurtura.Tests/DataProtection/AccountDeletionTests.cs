using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.DataProtection;

// After mother A deletes her account nothing points to her and nothing she wrote is left
// (design doc, "Account deletion"). Other people's data stays.
public sealed class AccountDeletionTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private sealed record Seeded(string[] TextsOfA, int PostOfAWithBsComment, int CommentOfBOnAsPost, int PostOfB);

    // A gets one of every kind of data the design doc lists for deletion.
    private async Task<Seeded> SeedEverythingAsync()
    {
        var a = Users.A;
        var b = Users.B;
        var texts = new List<string>();
        string Text(string label)
        {
            var text = TestData.Marker(label);
            texts.Add(text);
            return text;
        }

        // Wellbeing and baby.
        await Data.AddCheckInAsync(a.ProfileId, PolandTime.Today, Text("CheckIn"), shared: true);
        await Data.AddSleepAsync(a.BabyId, DateTime.UtcNow.AddHours(-8), TimeSpan.FromHours(2), Text("Sleep"), shared: true);
        await Data.AddFeedAsync(a.BabyId, DateTime.UtcNow.AddHours(-1), Text("Feed"), shared: false);

        // Appointments: an upcoming one (cancelled, P1 told without her name) and a past one with chat and note.
        var upcoming = await Data.AddAppointmentAsync(a.ProfileId, Users.P1.ProfileId, TestData.UpcomingDate(),
            AppointmentStatus.Confirmed, concern: Text("Concern"));
        var past = await Data.AddAppointmentAsync(a.ProfileId, Users.P1.ProfileId, TestData.PastDate(),
            AppointmentStatus.Completed, concern: Text("PastConcern"));
        await Data.AddChatMessageAsync(past, AppointmentParty.Mother, Text("Chat"));
        await Data.AddClosingNoteAsync(past, Text("ClosingNote"));
        await Data.AddNotificationAsync(a.UserId, NotificationType.RequestAccepted, Users.P1.DisplayName, upcoming);
        await Data.AddNotificationAsync(Users.P1.UserId, NotificationType.NewRequest, a.DisplayName, upcoming);

        // Forum: her post alone, her post with B's comment, her comment on B's post, reports, saved and hidden posts.
        await Data.AddPostAsync(a.ProfileId, Text("LonePostTitle"), Text("LonePostBody"));
        var postWithComment = await Data.AddPostAsync(a.ProfileId, Text("PostTitle"), Text("PostBody"));
        var commentOfB = await Data.AddCommentAsync(postWithComment, b.ProfileId, TestData.Marker("CommentOfB"));
        await Data.AddNotificationAsync(a.UserId, NotificationType.CommentOnYourPost, b.DisplayName,
            postId: postWithComment, commentId: commentOfB);

        var postOfB = await Data.AddPostAsync(b.ProfileId, TestData.Marker("TitleB"), TestData.Marker("BodyB"));
        var commentOfA = await Data.AddCommentAsync(postOfB, a.ProfileId, Text("CommentOfA"));
        await Data.AddNotificationAsync(b.UserId, NotificationType.CommentOnYourPost, a.DisplayName,
            postId: postOfB, commentId: commentOfA);
        // An older notification without the comment link, found by her name.
        await Data.AddNotificationAsync(b.UserId, NotificationType.CommentOnYourPost, a.DisplayName, postId: postOfB);

        var otherPostOfB = await Data.AddPostAsync(b.ProfileId, TestData.Marker("Title2B"), TestData.Marker("Body2B"));
        var otherCommentOfB = await Data.AddCommentAsync(otherPostOfB, b.ProfileId, TestData.Marker("Comment2B"));
        await Data.AddPostReportAsync(a.ProfileId, otherPostOfB, Text("ReportNote"));
        await Data.AddCommentReportAsync(a.ProfileId, otherCommentOfB, Text("CommentReportNote"));

        var guide = await Data.DbAsync(db => db.GuidanceContents.Select(g => g.Id).FirstAsync());
        await Data.DbAsync(async db =>
        {
            db.SavedGuidances.Add(new SavedGuidance { MotherProfileId = a.ProfileId, GuidanceContentId = guide, SavedAt = DateTime.UtcNow });
            db.SavedPosts.Add(new SavedPost { MotherProfileId = a.ProfileId, ForumPostId = postOfB, SavedAt = DateTime.UtcNow });
            db.HiddenPosts.Add(new HiddenPost { MotherProfileId = a.ProfileId, ForumPostId = otherPostOfB, HiddenAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        // A linked Google sign-in.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(a.UserId);
            var result = await users.AddLoginAsync(user!, new UserLoginInfo("Google", Text("GoogleKey"), "Google"));
            Assert.True(result.Succeeded);
        }

        return new Seeded(texts.ToArray(), postWithComment, commentOfB, postOfB);
    }

    [Fact]
    public async Task Delete_LeavesNoRowPointingToHer()
    {
        var a = Users.A;
        var seeded = await SeedEverythingAsync();
        // The scans below can find her data: before deletion every one of her texts is there.
        Assert.NotEmpty(await DbScan.RowsReferencingAsync("MotherProfiles", a.ProfileId));
        Assert.NotEmpty(await DbScan.RowsReferencingAsync("AspNetUsers", a.UserId));
        foreach (var text in new[] { a.UserId, a.Email, a.DisplayName }.Concat(seeded.TextsOfA))
        {
            Assert.NotEmpty(await DbScan.RowsContainingAsync(text));
        }

        using var client = await SignInAsync(a.Email);

        AssertRedirect(await client.PostAsync("/Mother/MyAccount/Delete", new() { ["confirmation"] = NurturaAppFactory.Password }),
            "/Account/Deleted");

        // 1. No foreign key points to her user id or her profile id.
        Assert.Empty(await DbScan.RowsReferencingAsync("AspNetUsers", a.UserId));
        Assert.Empty(await DbScan.RowsReferencingAsync("MotherProfiles", a.ProfileId));

        // 2. Her user id, email, display name and everything she wrote appear in no text column.
        foreach (var text in new[] { a.UserId, a.Email, a.DisplayName }.Concat(seeded.TextsOfA))
        {
            var hits = await DbScan.RowsContainingAsync(text);
            Assert.True(hits.Count == 0, $"Still found after deletion: {string.Join(", ", hits)}");
        }

        // 3. The Google login link is gone, so the email can be used again.
        Assert.False(await Data.DbAsync(db => db.Users.AnyAsync(u => u.Id == a.UserId)));
        Assert.Equal(0, await Data.DbAsync(db => db.Set<IdentityUserLogin<string>>().CountAsync()));

        // The allowed side: B's data stays, her comment keeps its place under A's cleared post, and P1 was told
        // about the cancelled appointment without A's name.
        Assert.True(await Data.DbAsync(db => db.MotherProfiles.AnyAsync(m => m.Id == Users.B.ProfileId)));
        Assert.True(await Data.DbAsync(db => db.ForumPosts.AnyAsync(p => p.Id == seeded.PostOfB)));
        Assert.True(await Data.DbAsync(db => db.ForumComments.AnyAsync(c => c.Id == seeded.CommentOfBOnAsPost)));
        var cleared = await Data.DbAsync(db => db.ForumPosts.AsNoTracking().SingleAsync(p => p.Id == seeded.PostOfAWithBsComment));
        Assert.Null(cleared.AuthorProfileId);
        Assert.NotNull(cleared.DeletedByAuthorAt);
        Assert.Equal(string.Empty, cleared.Title + cleared.Body);
        var notice = await Data.DbAsync(db => db.Notifications.AsNoTracking()
            .SingleAsync(n => n.UserId == Users.P1.UserId && n.Type == NotificationType.MotherDeletedAccount));
        Assert.DoesNotContain(a.DisplayName, notice.OtherName);
    }

    [Fact]
    public async Task Delete_WrongPassword_DeletesNothing()
    {
        var a = Users.A;
        await SeedEverythingAsync();
        var before = await DbScan.RowsReferencingAsync("MotherProfiles", a.ProfileId);
        using var client = await SignInAsync(a.Email);

        var response = await client.PostAsync("/Mother/MyAccount/Delete", new() { ["confirmation"] = "wrong-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await Data.DbAsync(db => db.Users.AnyAsync(u => u.Id == a.UserId)));
        Assert.Equal(before.Select(h => h.ToString()), (await DbScan.RowsReferencingAsync("MotherProfiles", a.ProfileId)).Select(h => h.ToString()));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Mother")).StatusCode);
    }
}
