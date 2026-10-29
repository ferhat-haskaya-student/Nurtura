using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Web.Models.Account;

namespace Nurtura.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AccountController(SignInManager<ApplicationUser> signInManager, IStringLocalizer<SharedResource> localizer)
    {
        _signInManager = signInManager;
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // One general message for a wrong email or password, so it does not reveal which one was wrong.
        var message = result.IsLockedOut ? "Account_LockedOut" : "Account_InvalidLogin";
        ModelState.AddModelError(string.Empty, _localizer[message]);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
