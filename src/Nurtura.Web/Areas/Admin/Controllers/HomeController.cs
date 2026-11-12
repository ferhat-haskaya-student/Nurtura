using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;

namespace Nurtura.Web.Areas.Admin.Controllers;

// The admin home. Numbers will live here; until then it opens the Professionals page.
[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(ProfessionalsController.Index), "Professionals");
    }
}
