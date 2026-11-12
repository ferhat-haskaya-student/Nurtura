using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Admin.Controllers;

// The admin's Reports page: the queue of reported forum items, Dismiss / Hide / Remove, and the "Hidden by
// moderator" list with Unhide. Admin role only, on every action. Reporters are never shown.
[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class ReportsController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

    private readonly ForumModerationService _moderationService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ReportsController(ForumModerationService moderationService, IStringLocalizer<SharedResource> localizer)
    {
        _moderationService = moderationService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await _moderationService.GetQueueAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(ModerationKind kind, int id) =>
        Done(Enum.IsDefined(kind) && await _moderationService.DismissAsync(kind, id), "AdminReports_Dismissed", nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(ModerationKind kind, int id) =>
        Done(Enum.IsDefined(kind) && await _moderationService.HideAsync(kind, id), "AdminReports_Hidden_" + kind, nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(ModerationKind kind, int id) =>
        Done(Enum.IsDefined(kind) && await _moderationService.RemoveAsync(kind, id), "AdminReports_Removed_" + kind, nameof(Index));

    [HttpGet]
    public async Task<IActionResult> Hidden()
    {
        return View(await _moderationService.GetHiddenAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unhide(ModerationKind kind, int id) =>
        Done(Enum.IsDefined(kind) && await _moderationService.UnhideAsync(kind, id), "AdminReports_Unhidden", nameof(Hidden));

    // Nothing changed (already handled, or gone): say so instead of a success line.
    private IActionResult Done(bool changed, string messageKey, string backTo)
    {
        if (changed)
        {
            TempData[StatusMessageKey] = _localizer[messageKey].Value;
        }
        else
        {
            TempData[ProblemMessageKey] = _localizer["AdminReports_NothingChanged"].Value;
        }

        return RedirectToAction(backTo);
    }
}
