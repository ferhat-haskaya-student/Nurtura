using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Controllers;

// My account and deleting it. Only ever the logged-in mother's own account: the user id comes from her sign-in,
// never from the URL.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class MyAccountController : Controller
{
    private const string ErrorKey = "DeleteError";

    private readonly AccountDeletionService _deletionService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public MyAccountController(
        AccountDeletionService deletionService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _deletionService = deletionService;
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var account = await _deletionService.GetMotherAccountAsync(UserId);
        return account is null ? NotFound() : View(account);
    }

    // The confirm page: what will be deleted, then her password (or her email for a Google-only account).
    [HttpGet]
    public async Task<IActionResult> Delete()
    {
        var account = await _deletionService.GetMotherAccountAsync(UserId);
        return account is null ? NotFound() : View(account);
    }

    // Wrong password or email: the page again, nothing deleted. Right: everything deleted, signed out.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(string? confirmation)
    {
        var user = await _userManager.GetUserAsync(User);
        var account = await _deletionService.GetMotherAccountAsync(UserId);
        if (user is null || account is null)
        {
            return NotFound();
        }

        var confirmed = await _userManager.HasPasswordAsync(user)
            ? !string.IsNullOrEmpty(confirmation) && await _userManager.CheckPasswordAsync(user, confirmation)
            : !string.IsNullOrWhiteSpace(confirmation) && string.Equals(confirmation.Trim(), user.Email, StringComparison.OrdinalIgnoreCase);
        if (!confirmed)
        {
            ViewData[ErrorKey] = _localizer[account.HasPassword ? "MyAccount_WrongPassword" : "MyAccount_WrongEmail"].Value;
            return View(nameof(Delete), account);
        }

        if (!await _deletionService.DeleteMotherAsync(UserId))
        {
            return NotFound();
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction("Deleted", "Account", new { area = "" });
    }

    private string UserId => _userManager.GetUserId(User)!;
}
