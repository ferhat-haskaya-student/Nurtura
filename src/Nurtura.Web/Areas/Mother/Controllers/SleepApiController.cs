using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother.Controllers;

// Start, Stop and Resume on the timer screen without a page reload (wwwroot/js/sleep-timer-api.js). It takes the same
// fields as the form and runs the same code (SleepTimerScreen), so every rule and message is the form's.
// 200: the new state. 422: refused by a rule, with the form's messages and the state as it is.
// Another mother's baby or sleep id gives 404. In the Mother area, so the onboarding filter runs for it too.
[ApiController]
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
[RequireAntiforgeryToken]
[Route("api/sleep")]
public class SleepApiController : ControllerBase
{
    private readonly SleepTimerScreen _screen;
    private readonly SleepService _sleepService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SleepApiController(
        SleepTimerScreen screen,
        SleepService sleepService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _screen = screen;
        _sleepService = sleepService;
        _userManager = userManager;
        _localizer = localizer;
    }

    [HttpPost(SleepCommands.Start)]
    public Task<IActionResult> Start([FromForm] int babyId, [FromForm] int? sleepId, [FromForm] SleepTimerViewModel model) =>
        RunAsync(SleepCommands.Start, babyId, sleepId, model);

    [HttpPost(SleepCommands.Stop)]
    public Task<IActionResult> Stop([FromForm] int babyId, [FromForm] int? sleepId, [FromForm] SleepTimerViewModel model) =>
        RunAsync(SleepCommands.Stop, babyId, sleepId, model);

    [HttpPost(SleepCommands.Resume)]
    public Task<IActionResult> Resume([FromForm] int babyId, [FromForm] int? sleepId, [FromForm] SleepTimerViewModel model) =>
        RunAsync(SleepCommands.Resume, babyId, sleepId, model);

    private async Task<IActionResult> RunAsync(string command, int babyId, int? sleepId, SleepTimerViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        var result = await _screen.RunAsync(userId, babyId, sleepId, command, model, ModelState);

        switch (result.Status)
        {
            case SleepCommandStatus.Saved:
                var saved = await _sleepService.GetAsync(userId, result.SavedId);
                return saved is null ? NotFound() : Ok(await AnswerAsync(result.Baby!, saved));
            case SleepCommandStatus.NotFound:
                return NotFound();
        }

        var problem = new ValidationProblemDetails(ModelState)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Extensions = { ["screen"] = await AnswerAsync(result.Baby!, result.Sleep) }
        };
        return UnprocessableEntity(problem);
    }

    private async Task<SleepTimerAnswer> AnswerAsync(Baby baby, SleepRecord? sleep)
    {
        var model = sleep is null ? await _screen.ForNewAsync(baby) : await _screen.ForSleepAsync(baby, sleep);
        var button = SleepTimerScreen.Button(model);
        return new SleepTimerAnswer
        {
            SleepId = model.SleepId,
            Command = button?.Command,
            CommandText = button is null ? null : _localizer[button.Value.TextKey].Value,
            RunningSince = model.RunningSince is null ? null : Milliseconds(model.RunningSince.Value),
            DurationText = model.Duration is null ? null : SleepText.Duration(_localizer, model.Duration.Value),
            StartDate = Text(model.StartDate),
            StartTime = Text(model.StartTime),
            EndDate = Text(model.EndDate),
            EndTime = Text(model.EndTime),
            Note = model.Note,
            IsShared = model.IsShared,
            IsLongRunning = model.IsLongRunning,
            EditUrl = sleep is null ? null : Url.Action("Edit", "Sleep", new { area = Roles.Mother, id = sleep.Id }),
            ResetUrl = sleep is null ? null : Url.Action("Reset", "Sleep", new { area = Roles.Mother, id = sleep.Id }),
            ServerNow = Milliseconds(DateTime.UtcNow)
        };
    }

    private static long Milliseconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();

    private static string? Text(DateOnly? date) => date?.ToString(DateFormats.Display, CultureInfo.InvariantCulture);

    private static string? Text(TimeOnly? time) => time?.ToString(TimeFormats.Display, CultureInfo.InvariantCulture);
}
