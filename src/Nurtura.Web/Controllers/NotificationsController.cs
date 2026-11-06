using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Controllers;

// The Notifications page for mothers and professionals. Only her own notifications.
[Authorize(Roles = Roles.Mother + "," + Roles.Professional)]
public class NotificationsController : Controller
{
    private readonly NotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(NotificationService notificationService, UserManager<ApplicationUser> userManager)
    {
        _notificationService = notificationService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await _notificationService.GetAllAsync(_userManager.GetUserId(User)!));
    }

    // Marks it read and goes to the appointment in her own list, or for a closing note to that consultation's
    // page. Another user's id gives 404.
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        if (await _notificationService.OpenAsync(_userManager.GetUserId(User)!, id) is not { } opened)
        {
            return NotFound();
        }

        var area = User.IsInRole(Roles.Mother) ? Roles.Mother : Roles.Professional;
        var url = opened.Type == NotificationType.ClosingNoteWritten
            ? Url.Action("Consultation", "Appointments", new { area, id = opened.AppointmentId })!
            : Url.Action("Index", "Appointments", new { area }) + "#appointment-" + opened.AppointmentId;
        return LocalRedirect(url);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllReadAsync(_userManager.GetUserId(User)!);
        return RedirectToAction(nameof(Index));
    }
}
