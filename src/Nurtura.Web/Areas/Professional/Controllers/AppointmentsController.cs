using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Professional.Models;

namespace Nurtura.Web.Areas.Professional.Controllers;

// "Appointments": requests, upcoming and past, with accept, reject, cancel and the consultation chat.
[Area(Roles.Professional)]
[Authorize(Roles = Roles.Professional)]
public class AppointmentsController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

    private readonly AppointmentService _appointmentService;
    private readonly ConsultationSummaryService _summaryService;
    private readonly ChatService _chatService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AppointmentsController(
        AppointmentService appointmentService,
        ConsultationSummaryService summaryService,
        ChatService chatService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _appointmentService = appointmentService;
        _summaryService = summaryService;
        _chatService = chatService;
        _userManager = userManager;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var appointments = await _appointmentService.GetForProfessionalAsync(userId);
        if (appointments is null)
        {
            return NotFound();
        }

        ViewData["Banners"] = await _appointmentService.GetProfessionalBannersAsync(userId);
        return View(appointments);
    }

    // The pre-consultation summary: only for her own requested or confirmed appointment, until the session ends.
    [HttpGet]
    public async Task<IActionResult> Summary(int id)
    {
        var summary = await _summaryService.GetAsync(_userManager.GetUserId(User)!, id);
        return summary is null ? NotFound() : View(summary);
    }

    // The chat of one of her confirmed appointments, with the summary as a second tab (until the session ends).
    [HttpGet]
    public async Task<IActionResult> Chat(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var room = await _chatService.GetRoomAsync(userId, id);
        if (room is null)
        {
            return NotFound();
        }

        return View(new ChatPageViewModel(room.Info, await _summaryService.GetAsync(userId, id)));
    }

    // One tap: Requested -> Confirmed.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        var accepted = await _appointmentService.AcceptAsync(_userManager.GetUserId(User)!, id);
        return Done(accepted, "Appointments_Accepted");
    }

    [HttpGet]
    public Task<IActionResult> Reject(int id) => MessagePage(id, AppointmentStatus.Requested, nameof(Reject));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(MessageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return await MessagePage(model, AppointmentStatus.Requested, nameof(Reject));
        }

        var rejected = await _appointmentService.RejectAsync(_userManager.GetUserId(User)!, model.Id, model.Message);
        return Done(rejected, "Appointments_Rejected");
    }

    [HttpGet]
    public Task<IActionResult> Cancel(int id) => MessagePage(id, AppointmentStatus.Confirmed, nameof(Cancel));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(MessageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return await MessagePage(model, AppointmentStatus.Confirmed, nameof(Cancel));
        }

        var cancelled = await _appointmentService.CancelByProfessionalAsync(_userManager.GetUserId(User)!, model.Id, model.Message);
        return Done(cancelled, "Appointments_Cancelled");
    }

    private Task<IActionResult> MessagePage(int id, AppointmentStatus status, string action) =>
        MessagePage(new MessageViewModel { Id = id }, status, action);

    // The reject or cancel page, shown only while the change is still allowed.
    private async Task<IActionResult> MessagePage(MessageViewModel model, AppointmentStatus status, string action)
    {
        model.Item = await _appointmentService.GetForProfessionalAsync(_userManager.GetUserId(User)!, model.Id, status);
        if (model.Item is null)
        {
            return Done(false, null);
        }

        ViewData["Action"] = action;
        return View("Message", model);
    }

    private RedirectToActionResult Done(bool changed, string? successKey)
    {
        if (changed)
        {
            TempData[StatusMessageKey] = _localizer[successKey!].Value;
        }
        else
        {
            TempData[ProblemMessageKey] = _localizer["Appointments_CantChange"].Value;
        }

        return RedirectToAction(nameof(Index));
    }
}
