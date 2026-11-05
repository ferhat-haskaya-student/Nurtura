using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// "My appointments": upcoming and past, with cancel and edit note. Rescheduling goes through Book.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class AppointmentsController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

    private readonly AppointmentService _appointmentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AppointmentsController(
        AppointmentService appointmentService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _appointmentService = appointmentService;
        _userManager = userManager;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var appointments = await _appointmentService.GetMineAsync(_userManager.GetUserId(User)!);
        return appointments is null ? NotFound() : View(appointments);
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
