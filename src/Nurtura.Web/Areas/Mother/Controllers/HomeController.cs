using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;

namespace Nurtura.Web.Areas.Mother.Controllers;

[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
