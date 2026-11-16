using System.Net;
using Microsoft.EntityFrameworkCore;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// Only the author can edit or delete her forum post or comment. Mother B tries it on A's, by id.
public sealed class ForumOwnershipTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private Task<string?> PostSnapshotAsync(int id) =>
        Data.DbAsync(db => db.ForumPosts.AsNoTracking().Where(p => p.Id == id)
            .Select(p => $"{p.Title}|{p.Body}|{p.ForumCategoryId}|{p.EditedAt:O}|{p.DeletedByAuthorAt:O}")
            .SingleOrDefaultAsync());

    private Task<string?> CommentSnapshotAsync(int id) =>
        Data.DbAsync(db => db.ForumComments.AsNoTracking().Where(c => c.Id == id)
            .Select(c => $"{c.Body}|{c.EditedAt:O}")
            .SingleOrDefaultAsync());

    private static Dictionary<string, string> PostForm(string title) => new()
    {
        ["CategoryId"] = "1", ["Language"] = "English", ["Title"] = title, ["Body"] = "Changed body"
    };

    [Fact]
    public async Task Post_OtherMotherEditOrDelete_Gets404AndPostUnchanged()
    {
        var post = await Data.AddPostAsync(Users.A.ProfileId, TestData.Marker("TitleA"), TestData.Marker("BodyA"));
        var before = await PostSnapshotAsync(post);
        using var b = await SignInAsync(Users.B.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/Mother/Forum/Edit/{post}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/Mother/Forum/Edit/{post}", PostForm("Changed by B"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/Mother/Forum/Delete/{post}", new())).StatusCode);

        Assert.Equal(before, await PostSnapshotAsync(post));
    }

    [Fact]
    public async Task Post_AuthorEdits_Saved()
    {
        var post = await Data.AddPostAsync(Users.A.ProfileId, TestData.Marker("TitleA"), TestData.Marker("BodyA"));
        using var a = await SignInAsync(Users.A.Email);

        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/Mother/Forum/Edit/{post}")).StatusCode);
        AssertRedirect(await a.PostAsync($"/Mother/Forum/Edit/{post}", PostForm("Changed by A")), $"/Mother/Forum/Post/{post}");

        Assert.StartsWith("Changed by A|", await PostSnapshotAsync(post));
    }

    [Fact]
    public async Task Comment_OtherMotherEditOrDelete_Gets404AndCommentUnchanged()
    {
        var post = await Data.AddPostAsync(Users.B.ProfileId, TestData.Marker("TitleB"), TestData.Marker("BodyB"));
        var comment = await Data.AddCommentAsync(post, Users.A.ProfileId, TestData.Marker("CommentA"));
        var before = await CommentSnapshotAsync(comment);
        using var b = await SignInAsync(Users.B.Email);

        // B wrote the post, but the comment is A's: owning the post gives no rights over its comments.
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/Mother/Forum/EditComment/{comment}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsync($"/Mother/Forum/EditComment/{comment}", new() { ["Body"] = "Changed by B" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/Mother/Forum/DeleteComment/{comment}", new())).StatusCode);

        Assert.Equal(before, await CommentSnapshotAsync(comment));
    }

    [Fact]
    public async Task Comment_AuthorDeletes_Removed()
    {
        var post = await Data.AddPostAsync(Users.B.ProfileId, TestData.Marker("TitleB"), TestData.Marker("BodyB"));
        var comment = await Data.AddCommentAsync(post, Users.A.ProfileId, TestData.Marker("CommentA"));
        using var a = await SignInAsync(Users.A.Email);

        AssertRedirect(await a.PostAsync($"/Mother/Forum/DeleteComment/{comment}", new()), $"/Mother/Forum/Post/{post}");

        Assert.Null(await CommentSnapshotAsync(comment));
    }
}
