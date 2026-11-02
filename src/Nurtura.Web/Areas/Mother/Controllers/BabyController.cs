using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// The baby section. Babies are reached by id in URLs; BabyService only ever finds her own,
// so another mother's id gives 404.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class BabyController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    // Remembers the baby she last chose. Only a preference: it is checked against her own babies on every read.
    private const string SelectedBabyCookie = "Nurtura.SelectedBaby";

    private readonly BabyService _babyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public BabyController(
        BabyService babyService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _babyService = babyService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // The selected baby's card: the id she tapped, else her last choice, else her youngest baby.
    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        var babies = await _babyService.GetBabiesAsync(UserId);

        var selected = id is null ? null : babies.SingleOrDefault(b => b.Id == id);
        if (id is not null)
        {
            if (selected is null)
            {
                return NotFound();
            }

            RememberSelection(selected.Id);
        }
        else if (int.TryParse(Request.Cookies[SelectedBabyCookie], out var rememberedId))
        {
            selected = babies.SingleOrDefault(b => b.Id == rememberedId);
        }

        selected ??= BabyService.Youngest(babies);
        var age = selected is null ? null : BabyAge.Calculate(selected.BirthDate, PolandTime.Today);
        return View(new BabySectionViewModel(babies, selected, age));
    }

    [HttpGet]
    public IActionResult Add()
    {
        return View(new BabyFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(BabyFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var id = await _babyService.AddAsync(UserId, model.ToDetails());
        if (id is null)
        {
            return NotFound();
        }

        RememberSelection(id.Value);
        TempData[StatusMessageKey] = _localizer["Baby_Added"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var baby = await _babyService.GetAsync(UserId, id);
        if (baby is null)
        {
            return NotFound();
        }

        return View(BabyFormModel.FromBaby(baby));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BabyFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return await _babyService.GetAsync(UserId, id) is null ? NotFound() : View(model);
        }

        if (!await _babyService.UpdateAsync(UserId, id, model.ToDetails()))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Baby_Saved"].Value;
        return RedirectToAction(nameof(Index), new { id });
    }

    // Confirmation page; nothing is deleted until she confirms.
    [HttpGet]
    public async Task<IActionResult> Remove(int id)
    {
        var baby = await _babyService.GetAsync(UserId, id);
        if (baby is null)
        {
            return NotFound();
        }

        return View(baby);
    }

    [HttpPost]
    [ActionName(nameof(Remove))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveConfirmed(int id)
    {
        if (!await _babyService.RemoveAsync(UserId, id))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["Baby_Removed"].Value;
        return RedirectToAction(nameof(Index));
    }

    private string UserId => _userManager.GetUserId(User)!;

    private void RememberSelection(int babyId)
    {
        Response.Cookies.Append(SelectedBabyCookie, babyId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
    }
}
