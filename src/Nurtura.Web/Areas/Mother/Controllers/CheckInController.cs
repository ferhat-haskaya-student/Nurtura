using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother.Controllers;

[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class CheckInController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    private readonly DailyCheckInService _checkInService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CheckInController(
        DailyCheckInService checkInService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _checkInService = checkInService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // Opens the check-in for the date: her saved one to edit, or an empty form.
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var today = PolandTime.Today;
        if (date is null || !DailyCheckInService.IsEditableDate(date.Value, today))
        {
            date = today;
        }

        var model = new CheckInViewModel { Date = date };
        var checkIn = await _checkInService.GetAsync(_userManager.GetUserId(User)!, date.Value);
        if (checkIn is not null)
        {
            model.Mood = checkIn.Mood;
            model.SleepQuality = checkIn.SleepQuality;
            model.Energy = checkIn.Energy;
            model.Stress = checkIn.Stress;
            model.FeelingTagIds = checkIn.FeelingTags.Select(t => t.FeelingTagId).ToList();
            model.Note = checkIn.Note;
            model.IsShared = checkIn.IsShared;
        }

        model.IsExisting = checkIn is not null;
        model.AvailableTags = await _checkInService.GetFeelingTagsAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckInViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;

        if (model.Date is not null && !DailyCheckInService.IsEditableDate(model.Date.Value, PolandTime.Today))
        {
            ModelState.AddModelError(nameof(model.Date), _localizer["Validation_CheckInDateRange"]);
        }

        var availableTags = await _checkInService.GetFeelingTagsAsync();
        if (model.FeelingTagIds.Any(id => availableTags.All(t => t.Id != id)))
        {
            ModelState.AddModelError(nameof(model.FeelingTagIds), _localizer["Validation_SelectOne"]);
        }

        if (!ModelState.IsValid)
        {
            model.IsExisting = model.Date is not null
                && await _checkInService.GetAsync(userId, model.Date.Value) is not null;
            model.AvailableTags = availableTags;
            return View(model);
        }

        if (!await _checkInService.SaveAsync(
                userId,
                new CheckInAnswers(
                    model.Date!.Value,
                    model.Mood!.Value,
                    model.SleepQuality!.Value,
                    model.Energy!.Value,
                    model.Stress!.Value,
                    model.FeelingTagIds,
                    model.Note,
                    model.IsShared)))
        {
            return NotFound();
        }

        // Back to the dashboard (mother home), which shows the message.
        TempData[StatusMessageKey] = _localizer["CheckIn_Saved"].Value;
        return RedirectToAction("Index", "Home", new { area = Roles.Mother });
    }

    // The week's summary and its 7 days. Opens on the current week.
    [HttpGet]
    public async Task<IActionResult> Week(DateOnly? date)
    {
        return View(await _checkInService.GetWeekAsync(_userManager.GetUserId(User)!, date ?? PolandTime.Today));
    }

    // A day from the week page: days she can still edit open the check-in form, older ones a read-only view.
    [HttpGet]
    public async Task<IActionResult> Day(DateOnly? date)
    {
        if (date is null)
        {
            return RedirectToAction(nameof(Week));
        }

        var display = date.Value.ToString(DateFormats.Display);
        if (DailyCheckInService.IsEditableDate(date.Value, PolandTime.Today))
        {
            return RedirectToAction(nameof(Index), new { date = display });
        }

        var day = await _checkInService.GetDayAsync(_userManager.GetUserId(User)!, date.Value);
        if (day is null)
        {
            return RedirectToAction(nameof(Week), new { date = display });
        }

        return View(day);
    }

    // Who can see a check-in can be changed on any day, even after ratings, tags and note are locked.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(DateOnly? date, bool isShared)
    {
        if (date is null || !await _checkInService.SetSharedAsync(_userManager.GetUserId(User)!, date.Value, isShared))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["CheckIn_SharingSaved"].Value;
        return RedirectToAction(nameof(Day), new { date = date.Value.ToString(DateFormats.Display) });
    }
}
