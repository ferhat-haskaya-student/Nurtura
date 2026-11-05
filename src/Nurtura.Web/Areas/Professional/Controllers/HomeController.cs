using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Professional.Controllers;

[Area(Roles.Professional)]
[Authorize(Roles = Roles.Professional)]
public class HomeController : Controller
{
    private readonly AppointmentService _appointmentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(AppointmentService appointmentService, UserManager<ApplicationUser> userManager)
    {
        _appointmentService = appointmentService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        // The consultation banner, from 24 hours before a confirmed consultation until its end.
        ViewData["Banners"] = await _appointmentService.GetProfessionalBannersAsync(_userManager.GetUserId(User)!);
        return View();
    }
}
