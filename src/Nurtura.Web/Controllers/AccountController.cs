using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
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
    private readonly MotherExternalLoginService _externalLoginService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    private const string LoginErrorKey = "LoginError";

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        MotherRegistrationService registrationService,
        ProfessionalRegistrationService professionalRegistrationService,
        MotherExternalLoginService externalLoginService,
        IStringLocalizer<SharedResource> localizer)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _registrationService = registrationService;
        _professionalRegistrationService = professionalRegistrationService;
        _externalLoginService = externalLoginService;
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

    // "Continue with Google": sends her to Google, which returns to ExternalLoginCallback.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToOwnHome();
        }

        var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        if (!schemes.Any(s => s.Name == GoogleDefaults.AuthenticationScheme))
        {
            return RedirectToAction(nameof(Login));
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl, string? remoteError)
    {
        // Set when she cancelled on Google or Google returned an error (see OnRemoteFailure in Program.cs).
        if (remoteError is not null)
        {
            return await ExternalLoginFailedAsync("Account_GoogleFailed");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return await ExternalLoginFailedAsync("Account_GoogleFailed");
        }

        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        // Not linked to any account yet: link it to her Mother account, or create one.
        if (!result.Succeeded && !result.IsLockedOut && !result.IsNotAllowed)
        {
            // Only an address Google has verified may be linked to an account, so nobody can take over
            // someone else's account with an unverified email.
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var emailVerified = string.Equals(
                info.Principal.FindFirstValue(GoogleClaimTypes.EmailVerified), "true", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(email) || !emailVerified)
            {
                return await ExternalLoginFailedAsync("Account_GoogleFailed");
            }

            var outcome = await _externalLoginService.LinkOrRegisterAsync(
                new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName),
                email,
                info.Principal.FindFirstValue(ClaimTypes.Name));

            switch (outcome)
            {
                case MotherExternalLoginOutcome.NotMother:
                    return await ExternalLoginFailedAsync("Account_UsePasswordLogin");
                case MotherExternalLoginOutcome.Failed:
                    return await ExternalLoginFailedAsync("Account_GoogleFailed");
            }

            result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        }

        if (result.IsLockedOut)
        {
            return await ExternalLoginFailedAsync("Account_LockedOut");
        }

        if (!result.Succeeded)
        {
            return await ExternalLoginFailedAsync("Account_GoogleFailed");
        }

        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        // A new mother lands on onboarding through the Mother area's onboarding redirect.
        var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        IList<string> roles = user is null ? [] : await _userManager.GetRolesAsync(user);
        return RedirectToAction("Index", "Home", new { area = RoleHome.AreaFor(roles) ?? "" });
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

    // Clears Google's temporary sign-in cookie and goes back to the login page with a message.
    private async Task<IActionResult> ExternalLoginFailedAsync(string messageKey)
    {
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        TempData[LoginErrorKey] = _localizer[messageKey].Value;
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToOwnHome() =>
        RedirectToAction("Index", "Home", new { area = RoleHome.AreaFor(User) ?? "" });
}
