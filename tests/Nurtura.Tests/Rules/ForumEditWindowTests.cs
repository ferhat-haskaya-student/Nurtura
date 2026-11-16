using Nurtura.Core.Entities;
using Nurtura.Core.Services;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// The forum's 30-minute edit window. ForumService reads the clock itself (no pure helper exists), so these run the
// service on nurtura_test with posts and comments written 29 and 31 minutes ago.
public sealed class ForumEditWindowTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private static readonly ForumPostInput Edit = new(1, Language.English, "Edited title", "Edited body");

    private Task<ForumSaveResult> EditPostAsync(int minutesOld) =>
        Data.ServiceAsync<ForumService, ForumSaveResult>(async forum =>
        {
            var post = await Data.AddPostAsync(Users.A.ProfileId, "Title", "Body", DateTime.UtcNow.AddMinutes(-minutesOld));
            return await forum.EditPostAsync(Users.A.UserId, post, Edit);
        });

    private Task<ForumSaveResult> EditCommentAsync(int minutesOld) =>
        Data.ServiceAsync<ForumService, ForumSaveResult>(async forum =>
        {
            var post = await Data.AddPostAsync(Users.B.ProfileId, "Title", "Body");
            var comment = await Data.AddCommentAsync(post, Users.A.ProfileId, "Comment", DateTime.UtcNow.AddMinutes(-minutesOld));
            return await forum.EditCommentAsync(Users.A.UserId, comment, "Edited comment");
        });

    [Fact]
    public async Task EditPost_29MinutesOld_Saved() =>
        Assert.Equal(ForumSaveStatus.Saved, (await EditPostAsync(29)).Status);

    [Fact]
    public async Task EditPost_31MinutesOld_Refused() =>
        Assert.Equal(ForumSaveStatus.EditWindowPassed, (await EditPostAsync(31)).Status);

    [Fact]
    public async Task EditComment_29MinutesOld_Saved() =>
        Assert.Equal(ForumSaveStatus.Saved, (await EditCommentAsync(29)).Status);

    [Fact]
    public async Task EditComment_31MinutesOld_Refused() =>
        Assert.Equal(ForumSaveStatus.EditWindowPassed, (await EditCommentAsync(31)).Status);
}
