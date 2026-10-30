using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Identity;
using Nurtura.Web.Models.Account;

namespace Nurtura.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MotherRegistrationService _registrationService;
    private readonly ProfessionalRegistrationService _professionalRegistrationService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        MotherRegistrationService registrationService,
        ProfessionalRegistrationService professionalRegistrationService,
        IStringLocalizer<SharedResource> localizer)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _registrationService = registrationService;
        _professionalRegistrationService = professionalRegistrationService;
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

            // User is not updated until the next request, so the roles are read from the database.
            var user = await _userManager.FindByEmailAsync(model.Email);
            IList<string> roles = user is null ? [] : await _userManager.GetRolesAsync(user);
            return RedirectToAction("Index", "Home", new { area = RoleHome.AreaFor(roles) ?? "" });
        }

        // One general message for a wrong email or password, so it does not reveal which one was wrong.
        var message = result.IsLockedOut ? "Account_LockedOut" : "Account_InvalidLogin";
        ModelState.AddModelError(string.Empty, _localizer[message]);
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToOwnHome();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToOwnHome();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _registrationService.RegisterAsync(model.DisplayName, model.Email, model.Password);
        if (!result.Succeeded)
        {
            AddRegistrationErrors(result);
            return View(model);
        }

        await _signInManager.SignInAsync(result.User!, isPersistent: false);
        return RedirectToAction("Index", "Home", new { area = Roles.Mother });
    }

    [HttpGet]
    public IActionResult RegisterProfessional()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToOwnHome();
        }

        return View(new ProfessionalRegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterProfessional(ProfessionalRegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToOwnHome();
        }

        if (model.Topics.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Topics), _localizer["Validation_SelectAtLeastOne"]);
        }

        if (model.Languages.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Languages), _localizer["Validation_SelectAtLeastOne"]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _professionalRegistrationService.RegisterAsync(new ProfessionalRegistration(
            model.DisplayName,
            model.Email,
            model.Password,
            model.ProfessionalType!.Value,
            model.Topics,
            model.Languages,
            model.Bio));
        if (!result.Succeeded)
        {
            AddRegistrationErrors(result);
            return View(model);
        }

        // Her area shows the "waiting for approval" page until the admin approves her.
        await _signInManager.SignInAsync(result.User!, isPersistent: false);
        return RedirectToAction("Index", "Home", new { area = Roles.Professional });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // A taken email gives both DuplicateUserName and DuplicateEmail with the same text; show it once.
    private void AddRegistrationErrors(RegistrationResult result)
    {
        foreach (var description in result.Errors.Select(e => e.Description).Distinct())
        {
            ModelState.AddModelError(string.Empty, description);
        }
    }

    private IActionResult RedirectToOwnHome() =>
        RedirectToAction("Index", "Home", new { area = RoleHome.AreaFor(User) ?? "" });
}
