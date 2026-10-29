using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;

namespace Nurtura.Web.Areas.Professional.Controllers;

[Area(Roles.Professional)]
[Authorize(Roles = Roles.Professional)]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
