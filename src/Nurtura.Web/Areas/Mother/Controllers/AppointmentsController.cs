using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// "My appointments": upcoming and past, with cancel, edit note, the consultation chat and past consultations. Rescheduling goes through Book.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class AppointmentsController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

    private readonly AppointmentService _appointmentService;
    private readonly ChatService _chatService;
    private readonly ConsultationService _consultationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AppointmentsController(
        AppointmentService appointmentService,
        ChatService chatService,
        ConsultationService consultationService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _appointmentService = appointmentService;
        _chatService = chatService;
        _consultationService = consultationService;
        _userManager = userManager;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var appointments = await _appointmentService.GetMineAsync(userId);
        if (appointments is null)
        {
            return NotFound();
        }

        ViewData["Banners"] = await _appointmentService.GetMotherBannersAsync(userId);
        return View(appointments);
    }

    // The chat of one of her confirmed appointments: before the room opens, while open, and read-only after.
    [HttpGet]
    public async Task<IActionResult> Chat(int id)
    {
        var room = await _chatService.GetRoomAsync(_userManager.GetUserId(User)!, id);
        return room is null ? NotFound() : View(room.Info);
    }

    // One of her past consultations (the session has ended): details, the closing note and the chat, read-only.
    [HttpGet]
    public async Task<IActionResult> Consultation(int id)
    {
        var consultation = await _consultationService.GetAsync(_userManager.GetUserId(User)!, id);
        return consultation is null ? NotFound() : View(consultation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        if (await _appointmentService.CancelByMotherAsync(_userManager.GetUserId(User)!, id))
        {
            TempData[StatusMessageKey] = _localizer["MyAppointments_Cancelled"].Value;
        }
        else
        {
            TempData[ProblemMessageKey] = _localizer["MyAppointments_CantChange"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditNote(int id)
    {
        var item = await _appointmentService.GetChangeableAsync(_userManager.GetUserId(User)!, id);
        if (item is null)
        {
            TempData[ProblemMessageKey] = _localizer["MyAppointments_CantChange"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View(new EditNoteViewModel { Id = id, Concern = item.Concern, Item = item });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNote(EditNoteViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        if (ModelState.IsValid)
        {
            if (await _appointmentService.EditNoteAsync(userId, model.Id, model.Concern!))
            {
                TempData[StatusMessageKey] = _localizer["MyAppointments_NoteSaved"].Value;
            }
            else
            {
                TempData[ProblemMessageKey] = _localizer["MyAppointments_CantChange"].Value;
            }

            return RedirectToAction(nameof(Index));
        }

        model.Item = await _appointmentService.GetChangeableAsync(userId, model.Id);
        if (model.Item is null)
        {
            TempData[ProblemMessageKey] = _localizer["MyAppointments_CantChange"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }
}
