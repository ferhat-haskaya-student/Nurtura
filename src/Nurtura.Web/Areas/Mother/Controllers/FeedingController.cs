using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother.Controllers;

// The feeding screen: a quick log after the feed, no timer. Feeds are reached only through her own
// babies (FeedingService), so another mother's baby or feed id gives 404.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class FeedingController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string DefaultTimeFormat = DateFormats.Display + " " + TimeFormats.Display;

    private readonly FeedingService _feedingService;
    private readonly BabyService _babyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public FeedingController(
        FeedingService feedingService,
        BabyService babyService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _feedingService = feedingService;
        _babyService = babyService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // The tile on the baby page opens this (id = baby): a new feed at now, with the type pre-selected.
    [HttpGet]
    public async Task<IActionResult> Log(int id)
    {
        var baby = await _babyService.GetAsync(UserId, id);
        var defaults = await _feedingService.GetDefaultsAsync(UserId, id);
        if (baby is null || defaults is null)
        {
            return NotFound();
        }

        var now = PolandTime.Now;
        var model = new FeedingViewModel
        {
            Date = DateOnly.FromDateTime(now),
            Time = TimeOnly.FromDateTime(now),
            DefaultTime = now.ToString(DefaultTimeFormat, CultureInfo.InvariantCulture),
            Type = defaults.Type,
            BottleContent = defaults.LastBottleContent
        };
        Display(model, baby, null, defaults);
        return View(model);
    }

    // One feed (id = feed), to edit or delete.
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var feed = await _feedingService.GetAsync(UserId, id);
        var baby = feed is null ? null : await _babyService.GetAsync(UserId, feed.BabyId);
        var defaults = feed is null ? null : await _feedingService.GetDefaultsAsync(UserId, feed.BabyId);
        if (feed is null || baby is null || defaults is null)
        {
            return NotFound();
        }

        var time = PolandTime.ToLocal(feed.Time);
        var model = new FeedingViewModel
        {
            Date = DateOnly.FromDateTime(time),
            Time = TimeOnly.FromDateTime(time),
            Type = feed.Type,
            BreastSide = feed.BreastSide,
            Minutes = feed.Minutes,
            AmountMl = feed.AmountMl,
            BottleContent = feed.BottleContent,
            Note = feed.Note,
            IsShared = feed.IsShared
        };
        Display(model, baby, feed, defaults);
        return View(nameof(Log), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int babyId, int? feedId, FeedingViewModel model)
    {
        var feed = feedId is null ? null : await _feedingService.GetAsync(UserId, feedId.Value);
        var baby = await _babyService.GetAsync(UserId, feed?.BabyId ?? babyId);
        if ((feedId is not null && feed is null) || baby is null)
        {
            return NotFound();
        }

        KeepOnlyChosenTypeFields(model);
        if (model.Type == FeedingType.Breast && model.BreastSide is null)
        {
            ModelState.AddModelError(nameof(model.BreastSide), _localizer["Feeding_SideRequired"]);
        }

        if (model.Type == FeedingType.Bottle && model.BottleContent is null)
        {
            ModelState.AddModelError(nameof(model.BottleContent), _localizer["Feeding_ContentRequired"]);
        }

        var timeUtc = TimeUtc(model, feed);
        if (ModelState.IsValid)
        {
            var details = new FeedingDetails(timeUtc!.Value, model.Type!.Value, model.BreastSide, model.Minutes,
                model.AmountMl, model.BottleContent, model.Note, model.IsShared);
            var result = feed is null
                ? await _feedingService.CreateAsync(UserId, baby.Id, details)
                : await _feedingService.UpdateAsync(UserId, feed.Id, details);

            switch (result.Status)
            {
                case FeedingSaveStatus.Saved:
                    TempData[StatusMessageKey] = _localizer["Feeding_Saved"].Value;
                    return RedirectToAction("Index", "Baby", new { id = baby.Id });
                case FeedingSaveStatus.NotFound:
                    return NotFound();
                case FeedingSaveStatus.InFuture:
                    ModelState.AddModelError(nameof(model.Time), _localizer["Feeding_InFuture"]);
                    break;
            }
        }

        Display(model, baby, feed, await _feedingService.GetDefaultsAsync(UserId, baby.Id));
        return View(nameof(Log), model);
    }

    // After the confirmation on the screen.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var babyId = await _feedingService.DeleteAsync(UserId, id);
        if (babyId is null)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Feeding_Deleted"].Value;
        return RedirectToAction("Index", "Baby", new { id = babyId });
    }

    private string UserId => _userManager.GetUserId(User)!;

    // The screen keeps every type's fields in the form; only the chosen type's count.
    // Their values and any errors on them (a stale hidden "0 minutes" under Bottle) are dropped.
    private void KeepOnlyChosenTypeFields(FeedingViewModel model)
    {
        if (model.Type != FeedingType.Breast)
        {
            model.BreastSide = null;
            model.Minutes = null;
            ModelState.Remove(nameof(model.BreastSide));
            ModelState.Remove(nameof(model.Minutes));
        }

        if (model.Type != FeedingType.Bottle)
        {
            model.AmountMl = null;
            model.BottleContent = null;
            ModelState.Remove(nameof(model.AmountMl));
            ModelState.Remove(nameof(model.BottleContent));
        }
    }

    // A new feed saved with the time left at its "now" uses the exact time of the tap.
    private DateTime? TimeUtc(FeedingViewModel model, FeedingRecord? feed)
    {
        if (model.Date is null || model.Time is null)
        {
            return null;
        }

        if (feed is null
            && model.DefaultTime == model.Date.Value.ToDateTime(model.Time.Value)
                .ToString(DefaultTimeFormat, CultureInfo.InvariantCulture))
        {
            return DateTime.UtcNow;
        }

        if (!PolandTime.TryToUtc(model.Date.Value, model.Time.Value, out var utc))
        {
            ModelState.AddModelError(nameof(model.Time), _localizer["Validation_TimeInvalid"]);
            return null;
        }

        return utc;
    }

    // What the screen shows besides the form: the baby, the feed being edited, the last side hint.
    private static void Display(FeedingViewModel model, Baby baby, FeedingRecord? feed, FeedingDefaults? defaults)
    {
        model.BabyId = baby.Id;
        model.BabyName = baby.Name;
        model.FeedId = feed?.Id;
        model.LastBreastSide = defaults?.LastBreastSide;
    }
}
