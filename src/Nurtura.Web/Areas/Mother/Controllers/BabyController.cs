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

    // Remembers the baby she last chose (also read by Guides). Only a preference: it is checked against her own babies on every read.
    internal const string SelectedBabyCookie = "Nurtura.SelectedBaby";

    private readonly BabyService _babyService;
    private readonly SleepService _sleepService;
    private readonly FeedingService _feedingService;
    private readonly BabyRoutineService _routineService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public BabyController(
        BabyService babyService,
        SleepService sleepService,
        FeedingService feedingService,
        BabyRoutineService routineService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _babyService = babyService;
        _sleepService = sleepService;
        _feedingService = feedingService;
        _routineService = routineService;
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
        if (selected is null)
        {
            return View(new BabySectionViewModel(babies, null, null, null, null, [], null));
        }

        var age = BabyAge.Calculate(selected.BirthDate, PolandTime.Today);
        var sleep = await _sleepService.GetOverviewAsync(UserId, selected.Id);
        var lastFeed = await _feedingService.GetLastAsync(UserId, selected.Id);
        var recentFeeds = await _feedingService.GetRecentAsync(UserId, selected.Id, BabyTimeline.SinceUtc());
        var timeline = BabyTimeline.Build(sleep!.Recent, recentFeeds);
        var routine = await _routineService.GetAsync(UserId, selected.Id);
        return View(new BabySectionViewModel(babies, selected, age, sleep, lastFeed, timeline, routine));
    }

    // The full routine summary of one of her babies over the last 7 days.
    [HttpGet]
    public async Task<IActionResult> Routine(int id)
    {
        var baby = await _babyService.GetAsync(UserId, id);
        var routine = baby is null ? null : await _routineService.GetAsync(UserId, id);
        if (baby is null || routine is null)
        {
            return NotFound();
        }

        return View(new RoutineViewModel(baby, routine));
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
