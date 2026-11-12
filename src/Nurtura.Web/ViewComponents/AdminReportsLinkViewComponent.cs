using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Services;

namespace Nurtura.Web.ViewComponents;

// The admin's "Reports" menu link with the number of reported items still open, worked out on every page load.
// The layout shows it only to the admin.
public class AdminReportsLinkViewComponent : ViewComponent
{
    private readonly ForumModerationService _moderationService;

    public AdminReportsLinkViewComponent(ForumModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        return View(await _moderationService.CountOpenItemsAsync());
    }
}
