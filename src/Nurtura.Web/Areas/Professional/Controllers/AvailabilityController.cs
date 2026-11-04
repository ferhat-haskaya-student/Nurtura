using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Professional.Controllers;

// "My availability": the week grid where she opens and closes her one-hour slots.
[Area(Roles.Professional)]
[Authorize(Roles = Roles.Professional)]
public class AvailabilityController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    private readonly AvailabilityService _availabilityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AvailabilityController(
        AvailabilityService availabilityService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _availabilityService = availabilityService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // Opens on the current week. With copy, the week's hours are filled from the week before (not saved yet).
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date, bool copy = false)
    {
        var week = await _availabilityService.GetWeekAsync(_userManager.GetUserId(User)!, date ?? PolandTime.Today, copy);
        return week is null ? NotFound() : View(week);
    }

    // Saves the whole week. Each "open" value is "{day of the week, 0 = Monday}-{hour}".
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DateOnly? weekStart, List<string> open)
    {
        if (weekStart is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var hours = new List<(DateOnly, int)>();
        foreach (var value in open)
        {
            var parts = value.Split('-');
            if (parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
                && offset is >= 0 and < 7)
            {
                hours.Add((weekStart.Value.AddDays(offset), hour));
            }
        }

        var count = await _availabilityService.SaveWeekAsync(_userManager.GetUserId(User)!, weekStart.Value, hours);
        if (count is null)
        {
            return RedirectToAction(nameof(Index));
        }

        TempData[StatusMessageKey] = _localizer[Plural.Key("Availability_Saved", count.Value), count.Value].Value;
        return RedirectToAction(nameof(Index), new { date = weekStart.Value.ToString(DateFormats.Display) });
    }
}
