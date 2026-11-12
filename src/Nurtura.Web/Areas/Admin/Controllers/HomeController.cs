using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Admin.Controllers;

// The admin home: Numbers. Counts only, never names or text.
[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class HomeController : Controller
{
    private readonly AdminNumbersService _numbersService;

    public HomeController(AdminNumbersService numbersService)
    {
        _numbersService = numbersService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await _numbersService.GetAsync());
    }
}
