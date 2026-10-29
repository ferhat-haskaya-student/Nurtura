using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;

namespace Nurtura.Web.Areas.Admin.Controllers;

[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
