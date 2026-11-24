using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother;

// The buttons on the timer screen: the value of the form's "command", and the last part of the API's address.
public static class SleepCommands
{
    public const string Start = "start";
    public const string Stop = "stop";
    public const string Resume = "resume";
    public const string Save = "save";
}

public enum SleepCommandStatus
{
    Saved,

    // The reasons are in the ModelState.
    Refused,

    // The sleep or the baby does not exist or is not hers.
    NotFound
}

// SavedId is set when saved. Baby and Sleep (as it was before the command; null for a new one) are set unless not found.
public sealed record SleepCommandResult(SleepCommandStatus Status, Baby? Baby = null, SleepRecord? Sleep = null, int SavedId = 0);

// What the timer screen does and shows, in one place for the form (SleepController) and the API (SleepApiController).
// The rules themselves are in SleepService.
public class SleepTimerScreen
{
    private const string DefaultStartFormat = DateFormats.Display + " " + TimeFormats.Display;

    private readonly SleepService _sleepService;
    private readonly BabyService _babyService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SleepTimerScreen(SleepService sleepService, BabyService babyService, IStringLocalizer<SharedResource> localizer)
    {
        _sleepService = sleepService;
        _babyService = babyService;
        _localizer = localizer;
    }

    // The big round button: Start a new sleep, Stop a running one, Resume a stopped one.
    // A stopped sleep that can no longer be resumed has none: only Save and Reset.
    public static (string Command, string TextKey)? Button(SleepTimerViewModel model) => model.State switch
    {
        SleepTimerState.Running => (SleepCommands.Stop, "Sleep_Stop"),
        SleepTimerState.Stopped => model.CanResume ? (SleepCommands.Resume, "Sleep_Resume") : null,
        _ => (SleepCommands.Start, "Sleep_Start")
    };

    // The screen for a new sleep, starting now.
    public async Task<SleepTimerViewModel> ForNewAsync(Baby baby)
    {
        var now = PolandTime.Now;
        var model = new SleepTimerViewModel
        {
            StartDate = DateOnly.FromDateTime(now),
            StartTime = TimeOnly.FromDateTime(now),
            DefaultStart = now.ToString(DefaultStartFormat, CultureInfo.InvariantCulture)
        };
        await DisplayAsync(model, baby, null);
        return model;
    }

    // The screen for one sleep: running, or stopped to edit, resume or discard.
    public async Task<SleepTimerViewModel> ForSleepAsync(Baby baby, SleepRecord sleep)
    {
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
        return model;
    }

    // One button. The command decides the end time: start and resume leave it empty (running), stop sets it to now,
    // save takes it from the form. Why it was refused is added to the ModelState.
    public async Task<SleepCommandResult> RunAsync(
        string userId,
        int babyId,
        int? sleepId,
        string command,
        SleepTimerViewModel model,
        ModelStateDictionary modelState)
    {
        var sleep = sleepId is null ? null : await _sleepService.GetAsync(userId, sleepId.Value);
        var baby = await _babyService.GetAsync(userId, sleep?.BabyId ?? babyId);
        if ((sleepId is not null && sleep is null) || baby is null)
        {
            return new SleepCommandResult(SleepCommandStatus.NotFound);
        }

        var startUtc = StartUtc(model, command, sleep, modelState);
        var endUtc = EndUtc(model, command, sleep, modelState);
        if (!modelState.IsValid)
        {
            return new SleepCommandResult(SleepCommandStatus.Refused, baby, sleep);
        }

        var input = new SleepInput(startUtc!.Value, endUtc, model.Note, model.IsShared);
        var result = sleep is null
            ? await _sleepService.CreateAsync(userId, baby.Id, input)
            : await _sleepService.UpdateAsync(userId, sleep.Id, input);

        switch (result.Status)
        {
            case SleepSaveStatus.Saved:
                return new SleepCommandResult(SleepCommandStatus.Saved, baby, sleep, result.Id!.Value);
            case SleepSaveStatus.NotFound:
                return new SleepCommandResult(SleepCommandStatus.NotFound);
        }

        // Stop in the same minute as the start: times are kept to the minute, so there is nothing to save yet.
        if (result.Status == SleepSaveStatus.EndBeforeStart && command == SleepCommands.Stop)
        {
            modelState.AddModelError(string.Empty, _localizer["Sleep_TooShort"]);
        }
        else
        {
            AddError(result, modelState);
        }

        return new SleepCommandResult(SleepCommandStatus.Refused, baby, sleep);
    }

    // What the screen shows comes from the database, never from the form: a refused Stop still shows a running sleep.
    public async Task DisplayAsync(SleepTimerViewModel model, Baby baby, SleepRecord? sleep)
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

    // Start on a new sleep with the start fields left at their "now" uses the exact time of the tap.
    private DateTime? StartUtc(SleepTimerViewModel model, string command, SleepRecord? sleep, ModelStateDictionary modelState)
    {
        if (model.StartDate is null || model.StartTime is null)
        {
            return null;
        }

        if (command == SleepCommands.Start && sleep is null
            && model.DefaultStart == model.StartDate.Value.ToDateTime(model.StartTime.Value)
                .ToString(DefaultStartFormat, CultureInfo.InvariantCulture))
        {
            return DateTime.UtcNow;
        }

        if (!PolandTime.TryToUtc(model.StartDate.Value, model.StartTime.Value, out var utc))
        {
            modelState.AddModelError(nameof(model.StartTime), _localizer["Validation_TimeInvalid"]);
            return null;
        }

        return utc;
    }

    private DateTime? EndUtc(SleepTimerViewModel model, string command, SleepRecord? sleep, ModelStateDictionary modelState)
    {
        switch (command)
        {
            case SleepCommands.Stop:
                return DateTime.UtcNow;
            case SleepCommands.Start or SleepCommands.Resume:
                return null;
        }

        // Save: a running sleep may be saved without an end (it keeps running); anything else needs one.
        if (model.EndDate is null && model.EndTime is null)
        {
            if (sleep is not null && sleep.EndTime is null)
            {
                return null;
            }

            modelState.AddModelError(nameof(model.EndTime), _localizer["Sleep_EndRequired"]);
            return null;
        }

        if (model.EndDate is null || model.EndTime is null)
        {
            modelState.AddModelError(nameof(model.EndTime), _localizer["Sleep_EndIncomplete"]);
            return null;
        }

        if (!PolandTime.TryToUtc(model.EndDate.Value, model.EndTime.Value, out var utc))
        {
            modelState.AddModelError(nameof(model.EndTime), _localizer["Validation_TimeInvalid"]);
            return null;
        }

        return utc;
    }

    private void AddError(SleepSaveResult result, ModelStateDictionary modelState)
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
        modelState.AddModelError(field, message);
    }
}
