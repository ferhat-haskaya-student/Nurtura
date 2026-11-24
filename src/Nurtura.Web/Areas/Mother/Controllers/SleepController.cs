using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// The sleep timer screen. Sleeps are reached only through her own babies (SleepService), so
// another mother's baby or sleep id gives 404. All rules are checked in SleepService.
// With JavaScript, Start, Stop and Resume go to SleepApiController instead; both use SleepTimerScreen.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class SleepController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    private readonly SleepTimerScreen _screen;
    private readonly SleepService _sleepService;
    private readonly BabyService _babyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SleepController(
        SleepTimerScreen screen,
        SleepService sleepService,
        BabyService babyService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _screen = screen;
        _sleepService = sleepService;
        _babyService = babyService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // The tile on the baby page opens this (id = baby): her running sleep, or a new one starting now.
    [HttpGet]
    public async Task<IActionResult> Timer(int id)
    {
        var baby = await _babyService.GetAsync(UserId, id);
        if (baby is null)
        {
            return NotFound();
        }

        var running = await _sleepService.GetRunningAsync(UserId, id);
        if (running is not null)
        {
            return RedirectToAction(nameof(Edit), new { id = running.Id });
        }

        return View(await _screen.ForNewAsync(baby));
    }

    // One sleep (id = sleep): running, or stopped to edit, resume or discard.
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var sleep = await _sleepService.GetAsync(UserId, id);
        var baby = sleep is null ? null : await _babyService.GetAsync(UserId, sleep.BabyId);
        if (sleep is null || baby is null)
        {
            return NotFound();
        }

        return View(nameof(Timer), await _screen.ForSleepAsync(baby, sleep));
    }

    // Every button on the timer screen.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int babyId, int? sleepId, string command, SleepTimerViewModel model)
    {
        if (command is not (SleepCommands.Start or SleepCommands.Stop or SleepCommands.Resume or SleepCommands.Save))
        {
            return BadRequest();
        }

        var result = await _screen.RunAsync(UserId, babyId, sleepId, command, model, ModelState);
        switch (result.Status)
        {
            case SleepCommandStatus.Saved when command == SleepCommands.Save:
                TempData[StatusMessageKey] = _localizer["Sleep_Saved"].Value;
                return RedirectToAction("Index", "Baby", new { id = result.Baby!.Id });
            case SleepCommandStatus.Saved:
                return RedirectToAction(nameof(Edit), new { id = result.SavedId });
            case SleepCommandStatus.NotFound:
                return NotFound();
        }

        await _screen.DisplayAsync(model, result.Baby!, result.Sleep);
        return View(nameof(Timer), model);
    }

    // Reset: discards the sleep (after the confirmation on the screen) and goes back to the baby page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(int id)
    {
        var babyId = await _sleepService.DeleteAsync(UserId, id);
        if (babyId is null)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Sleep_Discarded"].Value;
        return RedirectToAction("Index", "Baby", new { id = babyId });
    }

    private string UserId => _userManager.GetUserId(User)!;
}
