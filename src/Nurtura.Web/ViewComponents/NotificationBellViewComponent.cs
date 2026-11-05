using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.ViewComponents;

// The bell in the top menu with the unread count, worked out on every page load.
public class NotificationBellViewComponent : ViewComponent
{
    private readonly NotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationBellViewComponent(NotificationService notificationService, UserManager<ApplicationUser> userManager)
    {
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = _userManager.GetUserId(HttpContext.User);
        var count = userId is null ? 0 : await _notificationService.CountUnreadAsync(userId);
        return View(count);
    }
}
