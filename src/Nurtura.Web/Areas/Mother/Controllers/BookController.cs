using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// Booking: topic, free slots for the topic, a professional's profile, and the request.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class BookController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    private readonly BookingService _bookingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public BookController(
        BookingService bookingService,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _bookingService = bookingService;
        _userManager = userManager;
        _localizer = localizer;
    }

    // Step 1: the six topics.
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // Step 2: free slots for the topic, grouped by day. No language means any language.
    [HttpGet]
    public async Task<IActionResult> Slots(Topic? topic, Language? language)
    {
        if (!IsDefined(topic))
        {
            return RedirectToAction(nameof(Index));
        }

        if (language is not null && !Enum.IsDefined(language.Value))
        {
            language = null;
        }

        var days = await _bookingService.GetFreeSlotsAsync(_userManager.GetUserId(User)!, topic!.Value, language);
        if (days is null)
        {
            return NotFound();
        }

        ViewData["Topic"] = topic.Value;
        ViewData["Language"] = language;
        return View(days);
    }

    // An approved professional's profile, opened from a slot.
    [HttpGet]
    public async Task<IActionResult> Professional(int id, Topic? topic, Language? language)
    {
        var professional = await _bookingService.GetProfessionalAsync(id);
        if (professional is null)
        {
            return NotFound();
        }

        ViewData["Topic"] = IsDefined(topic) ? topic : null;
        ViewData["Language"] = language is not null && Enum.IsDefined(language.Value) ? language : null;
        return View(professional);
    }

    // Step 3: the request page for a slot. (Named RequestPage because Controller already has a Request property.)
    [HttpGet]
    [ActionName("Request")]
    public async Task<IActionResult> RequestPage(int slotId, Topic? topic)
    {
        if (!IsDefined(topic))
        {
            return RedirectToAction(nameof(Index));
        }

        var page = await _bookingService.GetRequestPageAsync(_userManager.GetUserId(User)!, slotId, topic!.Value);
        if (page is null)
        {
            return NotFound();
        }

        return View(new BookRequestViewModel { SlotId = slotId, Topic = topic, Page = page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Request")]
    public async Task<IActionResult> SendRequest(BookRequestViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        if (!IsDefined(model.Topic))
        {
            return RedirectToAction(nameof(Index));
        }

        if (ModelState.IsValid)
        {
            var result = await _bookingService.RequestAsync(
                userId,
                new BookingRequest(model.SlotId, model.Topic!.Value, model.Concern!, model.ShareCheckIns, model.ShareBabyId));

            if (result.Status == BookingStatus.Sent)
            {
                TempData[StatusMessageKey] = _localizer[
                    "Book_Sent",
                    result.ProfessionalName!,
                    BookText.Moment(_localizer, result.ConfirmBy!.Value)].Value;
                return RedirectToAction(nameof(Index));
            }

            if (result.Status == BookingStatus.Invalid)
            {
                return NotFound();
            }

            model.Problem = result.Status;
        }

        model.Page = await _bookingService.GetRequestPageAsync(userId, model.SlotId, model.Topic!.Value);
        if (model.Page is null)
        {
            return NotFound();
        }

        return View(model);
    }

    private static bool IsDefined(Topic? topic) => topic is not null && Enum.IsDefined(topic.Value);
}
