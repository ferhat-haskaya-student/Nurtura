using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// The mother home is her wellbeing dashboard.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class HomeController : Controller
{
    private readonly DailyCheckInService _checkInService;
    private readonly MotherProfileService _profileService;
    private readonly AppointmentService _appointmentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        DailyCheckInService checkInService,
        MotherProfileService profileService,
        AppointmentService appointmentService,
        UserManager<ApplicationUser> userManager)
    {
        _checkInService = checkInService;
        _profileService = profileService;
        _appointmentService = appointmentService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var displayName = await _profileService.GetDisplayNameAsync(userId);
        if (displayName is null)
        {
            return NotFound();
        }

        // The consultation banner, from 24 hours before a confirmed consultation until its end.
        ViewData["Banners"] = await _appointmentService.GetMotherBannersAsync(userId);
        return View(new DashboardViewModel(displayName, await _checkInService.GetChartAsync(userId)));
    }
}
