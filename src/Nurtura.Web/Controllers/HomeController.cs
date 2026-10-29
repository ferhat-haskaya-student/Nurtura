using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Web.Identity;
using Nurtura.Web.Models;

namespace Nurtura.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Logged-in users have their own home page per role.
        var area = RoleHome.AreaFor(User);
        if (area is not null)
        {
            return RedirectToAction("Index", "Home", new { area });
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
