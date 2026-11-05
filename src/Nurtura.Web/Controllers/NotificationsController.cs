using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    // Marks it read and goes to the appointment in her own list. Another user's id gives 404.
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var appointmentId = await _notificationService.OpenAsync(_userManager.GetUserId(User)!, id);
        if (appointmentId is null)
        {
            return NotFound();
        }

        var area = User.IsInRole(Roles.Mother) ? Roles.Mother : Roles.Professional;
        var url = Url.Action("Index", "Appointments", new { area }) + "#appointment-" + appointmentId;
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
