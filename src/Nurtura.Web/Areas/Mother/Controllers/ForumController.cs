using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// The mothers' forum. Mothers only: professionals and the admin get 403 from the role check. ForumService scopes
// everything to her own mother profile and checks every rule again, so another mother's id gives 404.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class ForumController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

    // Only a flag crosses the redirect: her own new text had an urgent word. The text itself is never kept.
    private const string UrgentKey = "ForumUrgent";

    private readonly ForumService _forumService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ForumController(
        ForumService forumService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _forumService = forumService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // The filters are in the query string; they are not personal. The search is not (see Search).
    [HttpGet]
    public async Task<IActionResult> Index(int? category, Language? language, ForumTab tab = ForumTab.All, bool hidden = false, int page = 1)
    {
        if (language is { } chosen && !Enum.IsDefined(chosen))
        {
            language = null;
        }

        if (!Enum.IsDefined(tab) || hidden)
        {
            tab = ForumTab.All;
        }

        var filter = new ForumFilter(category, language, tab, hidden, page);
        var list = await _forumService.GetListAsync(UserId, filter);
        if (list is null)
        {
            return NotFound();
        }

        return View(new ForumIndexViewModel(list, filter with { Page = list.Page }));
    }

    // Opened without a search (refresh after leaving, a bookmark): back to the list.
    [HttpGet]
    public IActionResult Search()
    {
        return RedirectToAction(nameof(Index));
    }

    // A POST with the results shown directly (no redirect), so the text never appears in a URL: not in browser
    // history, server logs or Referer headers. It is never stored or logged.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(string? search)
    {
        var results = await _forumService.SearchAsync(UserId, search);
        if (results is null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new ForumSearchViewModel(search!.Trim(), results));
    }

    [HttpGet]
    public async Task<IActionResult> Post(int id)
    {
        var post = await _forumService.GetPostAsync(UserId, id);
        if (post is null)
        {
            return NotFound();
        }

        var urgent = TempData[UrgentKey] is true;
        return View(new ForumPostPageViewModel(post, urgent, new ForumCommentFormModel { PostId = id }));
    }

    [HttpGet]
    public async Task<IActionResult> New()
    {
        // The post's language starts as the app's language.
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl" ? Language.Polish : Language.English;
        return View(new ForumPostFormModel { Language = language, Categories = await _forumService.GetCategoriesAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ForumPostFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return await PostForm(model);
        }

        var result = await _forumService.CreatePostAsync(UserId, model.ToInput());
        if (result.Status == ForumSaveStatus.Invalid)
        {
            ModelState.AddModelError(string.Empty, _localizer["Forum_CheckForm"]);
            return await PostForm(model);
        }

        if (result.Status != ForumSaveStatus.Saved)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Forum_PostPublished"].Value;
        TempData[UrgentKey] = result.Urgent;
        return RedirectToAction(nameof(Post), new { id = result.PostId });
    }

    // Only her own post, only within 30 minutes of posting.
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _forumService.GetPostEditAsync(UserId, id);
        if (post is null)
        {
            return await PostEditNotAllowed(id);
        }

        return View(new ForumPostFormModel
        {
            Id = post.Id,
            CategoryId = post.CategoryId,
            Language = post.Language,
            Title = post.Title,
            Body = post.Body,
            EditableUntil = post.EditableUntil,
            Categories = await _forumService.GetCategoriesAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ForumPostFormModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            var current = await _forumService.GetPostEditAsync(UserId, id);
            if (current is null)
            {
                return await PostEditNotAllowed(id);
            }

            model.EditableUntil = current.EditableUntil;
            return await PostForm(model);
        }

        var result = await _forumService.EditPostAsync(UserId, id, model.ToInput());
        switch (result.Status)
        {
            case ForumSaveStatus.Saved:
                TempData[StatusMessageKey] = _localizer["Forum_PostUpdated"].Value;
                return RedirectToAction(nameof(Post), new { id });
            case ForumSaveStatus.Invalid:
                ModelState.AddModelError(string.Empty, _localizer["Forum_CheckForm"]);
                model.EditableUntil = (await _forumService.GetPostEditAsync(UserId, id))?.EditableUntil;
                return await PostForm(model);
            case ForumSaveStatus.EditWindowPassed:
                return await PostEditNotAllowed(id);
            default:
                return NotFound();
        }
    }

    // Her own post, at any time.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _forumService.DeletePostAsync(UserId, id))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Forum_PostDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(int id, ForumCommentFormModel model)
    {
        model.PostId = id;
        if (!ModelState.IsValid)
        {
            var post = await _forumService.GetPostAsync(UserId, id);
            if (post is null)
            {
                return NotFound();
            }

            return View(nameof(Post), new ForumPostPageViewModel(post, false, model));
        }

        var result = await _forumService.AddCommentAsync(UserId, id, model.Body);
        switch (result.Status)
        {
            case ForumSaveStatus.Saved:
                TempData[StatusMessageKey] = _localizer["Forum_CommentPublished"].Value;
                TempData[UrgentKey] = result.Urgent;
                return RedirectToAction(nameof(Post), new { id });
            case ForumSaveStatus.Closed:
                TempData[ProblemMessageKey] = _localizer["Forum_CommentsClosed"].Value;
                return RedirectToAction(nameof(Post), new { id });
            case ForumSaveStatus.Invalid:
                var post = await _forumService.GetPostAsync(UserId, id);
                if (post is null)
                {
                    return NotFound();
                }

                ModelState.AddModelError(nameof(ForumCommentFormModel.Body), _localizer["Forum_CheckForm"]);
                return View(nameof(Post), new ForumPostPageViewModel(post, false, model));
            default:
                return NotFound();
        }
    }

    // Only her own comment, only within 30 minutes of writing it.
    [HttpGet]
    public async Task<IActionResult> EditComment(int id)
    {
        var comment = await _forumService.GetCommentEditAsync(UserId, id);
        if (comment is null)
        {
            return await CommentEditNotAllowed(id);
        }

        return View(new ForumCommentFormModel
        {
            Id = comment.Id,
            PostId = comment.PostId,
            Body = comment.Body,
            EditableUntil = comment.EditableUntil
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(int id, ForumCommentFormModel model)
    {
        model.Id = id;
        var current = await _forumService.GetCommentEditAsync(UserId, id);
        if (current is null)
        {
            return await CommentEditNotAllowed(id);
        }

        model.PostId = current.PostId;
        model.EditableUntil = current.EditableUntil;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _forumService.EditCommentAsync(UserId, id, model.Body);
        switch (result.Status)
        {
            case ForumSaveStatus.Saved:
                TempData[StatusMessageKey] = _localizer["Forum_CommentUpdated"].Value;
                return RedirectToAction(nameof(Post), null, new { id = result.PostId }, "comment-" + id);
            case ForumSaveStatus.Invalid:
                ModelState.AddModelError(nameof(ForumCommentFormModel.Body), _localizer["Forum_CheckForm"]);
                return View(model);
            case ForumSaveStatus.EditWindowPassed:
                TempData[ProblemMessageKey] = _localizer["Forum_EditWindowPassed"].Value;
                return RedirectToAction(nameof(Post), null, new { id = result.PostId }, "comment-" + id);
            default:
                return NotFound();
        }
    }

    // Her own comment, at any time.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var (found, postId) = await _forumService.DeleteCommentAsync(UserId, id);
        if (!found)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Forum_CommentDeleted"].Value;
        return postId is null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Post), new { id = postId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id) =>
        Mark(await _forumService.SaveAsync(UserId, id), id, "Forum_SavedMessage");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsave(int id) =>
        Mark(await _forumService.UnsaveAsync(UserId, id), id, "Forum_UnsavedMessage");

    // Hiding takes her back to the list, where the post is no longer shown.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(int id)
    {
        if (!await _forumService.HideAsync(UserId, id))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Forum_HiddenMessage"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unhide(int id) =>
        Mark(await _forumService.UnhideAsync(UserId, id), id, "Forum_UnhiddenMessage");

    [HttpGet]
    public async Task<IActionResult> ReportPost(int id) =>
        ReportForm(await _forumService.GetPostReportTargetAsync(UserId, id), new ForumReportFormModel { Id = id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportPost(int id, ForumReportFormModel model)
    {
        model.Id = id;
        model.IsComment = false;
        var target = await _forumService.GetPostReportTargetAsync(UserId, id);
        if (target is null || target.AlreadyReported || !ModelState.IsValid)
        {
            return ReportForm(target, model);
        }

        return ReportDone(await _forumService.ReportPostAsync(UserId, id, model.Reason!.Value, model.Note), target.PostId);
    }

    [HttpGet]
    public async Task<IActionResult> ReportComment(int id) =>
        ReportForm(await _forumService.GetCommentReportTargetAsync(UserId, id), new ForumReportFormModel { Id = id, IsComment = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportComment(int id, ForumReportFormModel model)
    {
        model.Id = id;
        model.IsComment = true;
        var target = await _forumService.GetCommentReportTargetAsync(UserId, id);
        if (target is null || target.AlreadyReported || !ModelState.IsValid)
        {
            return ReportForm(target, model);
        }

        return ReportDone(await _forumService.ReportCommentAsync(UserId, id, model.Reason!.Value, model.Note), target.PostId);
    }

    private async Task<IActionResult> PostForm(ForumPostFormModel model)
    {
        model.Categories = await _forumService.GetCategoriesAsync();
        return View(model.Id == 0 ? nameof(New) : nameof(Edit), model);
    }

    // Not hers (or missing): 404. Hers, but the 30 minutes have passed: back to the post with a message.
    private async Task<IActionResult> PostEditNotAllowed(int id)
    {
        if (!await _forumService.IsOwnPostAsync(UserId, id))
        {
            return NotFound();
        }

        TempData[ProblemMessageKey] = _localizer["Forum_EditWindowPassed"].Value;
        return RedirectToAction(nameof(Post), new { id });
    }

    private async Task<IActionResult> CommentEditNotAllowed(int id)
    {
        if (await _forumService.OwnCommentPostIdAsync(UserId, id) is not { } postId)
        {
            return NotFound();
        }

        TempData[ProblemMessageKey] = _localizer["Forum_EditWindowPassed"].Value;
        return RedirectToAction(nameof(Post), null, new { id = postId }, "comment-" + id);
    }

    private IActionResult Mark(bool done, int id, string messageKey)
    {
        if (!done)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer[messageKey].Value;
        return RedirectToAction(nameof(Post), new { id });
    }

    // Shows the report form; already reported: back to the post with a message; not allowed: 404.
    private IActionResult ReportForm(ReportTarget? target, ForumReportFormModel model)
    {
        if (target is null)
        {
            return NotFound();
        }

        if (target.AlreadyReported)
        {
            TempData[StatusMessageKey] = _localizer["Forum_AlreadyReported"].Value;
            return RedirectToAction(nameof(Post), new { id = target.PostId });
        }

        model.PostId = target.PostId;
        return View("Report", model);
    }

    private IActionResult ReportDone(ReportResult result, int postId)
    {
        if (result == ReportResult.NotAllowed)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer[result == ReportResult.Reported ? "Forum_ReportThanks" : "Forum_AlreadyReported"].Value;
        return RedirectToAction(nameof(Post), new { id = postId });
    }

    private string UserId => _userManager.GetUserId(User)!;
}
