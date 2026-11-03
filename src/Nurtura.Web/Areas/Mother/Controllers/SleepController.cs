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

// The sleep timer screen. Sleeps are reached only through her own babies (SleepService), so
// another mother's baby or sleep id gives 404. All rules are checked in SleepService.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class SleepController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string DefaultStartFormat = DateFormats.Display + " " + TimeFormats.Display;

    private const string CommandStart = "start";
    private const string CommandStop = "stop";
    private const string CommandResume = "resume";
    private const string CommandSave = "save";

    private readonly SleepService _sleepService;
    private readonly BabyService _babyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SleepController(
        SleepService sleepService,
        BabyService babyService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
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

        var now = PolandTime.Now;
        var model = new SleepTimerViewModel
        {
            StartDate = DateOnly.FromDateTime(now),
            StartTime = TimeOnly.FromDateTime(now),
            DefaultStart = now.ToString(DefaultStartFormat, CultureInfo.InvariantCulture)
        };
        await DisplayAsync(model, baby, null);
        return View(model);
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

        var start = PolandTime.ToLocal(sleep.StartTime);
        var end = sleep.EndTime is null ? (DateTime?)null : PolandTime.ToLocal(sleep.EndTime.Value);
        var model = new SleepTimerViewModel
        {
            StartDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndDate = end is null ? null : DateOnly.FromDateTime(end.Value),
            EndTime = end is null ? null : TimeOnly.FromDateTime(end.Value),
            Note = sleep.Note,
            IsShared = sleep.IsShared
        };
        await DisplayAsync(model, baby, sleep);
        return View(nameof(Timer), model);
    }

    // Every button on the timer screen. The command decides the end time:
    // start and resume leave it empty (running), stop sets it to now, save takes it from the form.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int babyId, int? sleepId, string command, SleepTimerViewModel model)
    {
        if (command is not (CommandStart or CommandStop or CommandResume or CommandSave))
        {
            return BadRequest();
        }

        var sleep = sleepId is null ? null : await _sleepService.GetAsync(UserId, sleepId.Value);
        var baby = await _babyService.GetAsync(UserId, sleep?.BabyId ?? babyId);
        if ((sleepId is not null && sleep is null) || baby is null)
        {
            return NotFound();
        }

        var startUtc = StartUtc(model, command, sleep);
        var endUtc = EndUtc(model, command, sleep);
        if (!ModelState.IsValid)
        {
            await DisplayAsync(model, baby, sleep);
            return View(nameof(Timer), model);
        }

        var input = new SleepInput(startUtc!.Value, endUtc, model.Note, model.IsShared);
        var result = sleep is null
            ? await _sleepService.CreateAsync(UserId, baby.Id, input)
            : await _sleepService.UpdateAsync(UserId, sleep.Id, input);

        switch (result.Status)
        {
            case SleepSaveStatus.Saved when command == CommandSave:
                TempData[StatusMessageKey] = _localizer["Sleep_Saved"].Value;
                return RedirectToAction("Index", "Baby", new { id = baby.Id });
            case SleepSaveStatus.Saved:
                return RedirectToAction(nameof(Edit), new { id = result.Id });
            case SleepSaveStatus.NotFound:
                return NotFound();
        }

        // Stop in the same minute as the start: times are kept to the minute, so there is nothing to save yet.
        if (result.Status == SleepSaveStatus.EndBeforeStart && command == CommandStop)
        {
            ModelState.AddModelError(string.Empty, _localizer["Sleep_TooShort"]);
        }
        else
        {
            AddError(result);
        }

        await DisplayAsync(model, baby, sleep);
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

    // Start on a new sleep with the start fields left at their "now" uses the exact time of the tap.
    private DateTime? StartUtc(SleepTimerViewModel model, string command, SleepRecord? sleep)
    {
        if (model.StartDate is null || model.StartTime is null)
        {
            return null;
        }

        if (command == CommandStart && sleep is null
            && model.DefaultStart == model.StartDate.Value.ToDateTime(model.StartTime.Value)
                .ToString(DefaultStartFormat, CultureInfo.InvariantCulture))
        {
            return DateTime.UtcNow;
        }

        if (!PolandTime.TryToUtc(model.StartDate.Value, model.StartTime.Value, out var utc))
        {
            ModelState.AddModelError(nameof(model.StartTime), _localizer["Validation_TimeInvalid"]);
            return null;
        }

        return utc;
    }

    private DateTime? EndUtc(SleepTimerViewModel model, string command, SleepRecord? sleep)
    {
        switch (command)
        {
            case CommandStop:
                return DateTime.UtcNow;
            case CommandStart or CommandResume:
                return null;
        }

        // Save: a running sleep may be saved without an end (it keeps running); anything else needs one.
        if (model.EndDate is null && model.EndTime is null)
        {
            if (sleep is not null && sleep.EndTime is null)
            {
                return null;
            }

            ModelState.AddModelError(nameof(model.EndTime), _localizer["Sleep_EndRequired"]);
            return null;
        }

        if (model.EndDate is null || model.EndTime is null)
        {
            ModelState.AddModelError(nameof(model.EndTime), _localizer["Sleep_EndIncomplete"]);
            return null;
        }

        if (!PolandTime.TryToUtc(model.EndDate.Value, model.EndTime.Value, out var utc))
        {
            ModelState.AddModelError(nameof(model.EndTime), _localizer["Validation_TimeInvalid"]);
            return null;
        }

        return utc;
    }

    private void AddError(SleepSaveResult result)
    {
        var (field, message) = result.Status switch
        {
            SleepSaveStatus.InFuture => (string.Empty, _localizer["Sleep_InFuture"].Value),
            SleepSaveStatus.EndBeforeStart => (nameof(SleepTimerViewModel.EndTime), _localizer["Sleep_EndBeforeStart"].Value),
            SleepSaveStatus.TooLong => (nameof(SleepTimerViewModel.EndTime), _localizer["Sleep_TooLong"].Value),
            SleepSaveStatus.AlreadyRunning => (string.Empty, _localizer["Sleep_AlreadyRunning"].Value),
            SleepSaveStatus.ResumeNotAllowed => (string.Empty, _localizer["Sleep_ResumeNotAllowed"].Value),
            SleepSaveStatus.Overlap => (string.Empty, _localizer["Sleep_Overlap", SleepText.WithDate(_localizer, result.Clash!)].Value),
            SleepSaveStatus.NoteTooLong => (nameof(SleepTimerViewModel.Note),
                _localizer["Validation_MaxLength", _localizer["Sleep_Note"], SleepRecord.NoteMaxLength].Value),
            _ => throw new InvalidOperationException($"Unexpected sleep save status {result.Status}.")
        };
        ModelState.AddModelError(field, message);
    }

    // What the screen shows comes from the database, never from the form: a refused Stop still shows a running sleep.
    private async Task DisplayAsync(SleepTimerViewModel model, Baby baby, SleepRecord? sleep)
    {
        model.BabyId = baby.Id;
        model.BabyName = baby.Name;
        model.SleepId = sleep?.Id;
        model.State = sleep is null ? SleepTimerState.New
            : sleep.EndTime is null ? SleepTimerState.Running
            : SleepTimerState.Stopped;
        model.RunningSince = sleep?.EndTime is null ? sleep?.StartTime : null;
        model.IsLongRunning = model.RunningSince is not null
            && DateTime.UtcNow - model.RunningSince.Value >= SleepRecord.LongRunning;
        model.Duration = sleep?.EndTime - sleep?.StartTime;
        model.CanResume = sleep is not null && await _sleepService.CanResumeAsync(sleep);
    }
}
