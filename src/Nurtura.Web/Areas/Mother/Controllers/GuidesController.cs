using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// Guidance pages: the Guides list, her saved pages, one page, Save and Unsave.
// Pages are shared content; GuidanceService scopes everything saved to her own profile.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class GuidesController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    private readonly GuidanceService _guidanceService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GuidesController(
        GuidanceService guidanceService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _guidanceService = guidanceService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // Suggested for you, then For you and For your baby. Suggestions use the baby she last chose.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int? babyId = int.TryParse(Request.Cookies[BabyController.SelectedBabyCookie], out var remembered)
            ? remembered
            : null;
        return View(await _guidanceService.GetListAsync(UserId, babyId));
    }

    [HttpGet]
    public async Task<IActionResult> Saved()
    {
        return View(await _guidanceService.GetSavedAsync(UserId));
    }

    // One guidance page; a missing or unpublished id gives 404.
    [HttpGet]
    public async Task<IActionResult> Article(int id)
    {
        var page = await _guidanceService.GetPageAsync(UserId, id);
        if (page is null)
        {
            return NotFound();
        }

        return View(new GuidePageViewModel(page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id)
    {
        if (!await _guidanceService.SaveAsync(UserId, id))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Guides_SavedMessage"].Value;
        return RedirectToAction(nameof(Article), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsave(int id)
    {
        if (!await _guidanceService.UnsaveAsync(UserId, id))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Guides_UnsavedMessage"].Value;
        return RedirectToAction(nameof(Article), new { id });
    }

    private string UserId => _userManager.GetUserId(User)!;
}
