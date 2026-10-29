using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class OnboardingController : Controller
{
    private readonly MotherOnboardingService _onboardingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public OnboardingController(
        MotherOnboardingService onboardingService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _onboardingService = onboardingService;
        _userManager = userManager;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var completed = await _onboardingService.IsCompletedAsync(_userManager.GetUserId(User)!);
        if (completed is null)
        {
            return NotFound();
        }

        if (completed.Value)
        {
            return RedirectToMotherHome();
        }

        return View(new OnboardingViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OnboardingViewModel model)
    {
        if (model.Concerns.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Concerns), _localizer["Validation_SelectAtLeastOne"]);
        }

        if (model.SupportTypes.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SupportTypes), _localizer["Validation_SelectAtLeastOne"]);
        }

        if (model.BabyBirthDate > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(model.BabyBirthDate), _localizer["Validation_DateNotInFuture"]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // False means she was already onboarded (or has no profile); either way the form is done.
        await _onboardingService.CompleteAsync(
            _userManager.GetUserId(User)!,
            new OnboardingAnswers(
                model.Concerns,
                model.SupportTypes,
                model.IsFirstBaby!.Value,
                model.HelpAtHome!.Value,
                model.BabyName,
                model.BabyBirthDate!.Value,
                model.BirthType!.Value,
                model.BirthWeek!.Value,
                model.FeedingType!.Value));

        return RedirectToMotherHome();
    }

    private IActionResult RedirectToMotherHome() =>
        RedirectToAction("Index", "Home", new { area = Roles.Mother });
}
