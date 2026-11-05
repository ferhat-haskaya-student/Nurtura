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
// With "reschedule" (one of her appointments), the same pages pick a new time for that appointment:
// the topic is the appointment's, and it is cancelled only when the new request is sent.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class BookController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

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
    public async Task<IActionResult> Slots(Topic? topic, Language? language, int? reschedule)
    {
        var userId = _userManager.GetUserId(User)!;
        RescheduleInfo? info = null;
        if (reschedule is not null)
        {
            info = await _bookingService.GetRescheduleAsync(userId, reschedule.Value);
            if (info is null)
            {
                return CantChange();
            }

            topic = info.Topic;
        }

        if (!IsDefined(topic))
        {
            return RedirectToAction(nameof(Index));
        }

        if (language is not null && !Enum.IsDefined(language.Value))
        {
            language = null;
        }

        var days = await _bookingService.GetFreeSlotsAsync(userId, topic!.Value, language, info?.AppointmentId);
        if (days is null)
        {
            return NotFound();
        }

        var model = info is null
            ? new BookSlotsViewModel { Topic = topic.Value, Language = language, Days = days }
            : new BookSlotsViewModel
            {
                Topic = topic.Value,
                Language = language,
                Reschedule = info,
                SameProfessional = Split(days, s => s.Professional.Id == info.ProfessionalId),
                Days = Split(days, s => s.Professional.Id != info.ProfessionalId)
            };
        return View(model);
    }

    // An approved professional's profile, opened from a slot.
    [HttpGet]
    public async Task<IActionResult> Professional(int id, Topic? topic, Language? language, int? reschedule)
    {
        var professional = await _bookingService.GetProfessionalAsync(id);
        if (professional is null)
        {
            return NotFound();
        }

        ViewData["Topic"] = IsDefined(topic) ? topic : null;
        ViewData["Language"] = language is not null && Enum.IsDefined(language.Value) ? language : null;
        ViewData["Reschedule"] = reschedule;
        return View(professional);
    }

    // Step 3: the request page for a slot. (Named RequestPage because Controller already has a Request property.)
    [HttpGet]
    [ActionName("Request")]
    public async Task<IActionResult> RequestPage(int slotId, Topic? topic, int? reschedule)
    {
        var userId = _userManager.GetUserId(User)!;
        RescheduleInfo? info = null;
        if (reschedule is not null)
        {
            info = await _bookingService.GetRescheduleAsync(userId, reschedule.Value);
            if (info is null)
            {
                return CantChange();
            }

            topic = info.Topic;

            // The form shows the appointment's topic, not whatever came in the URL.
            ModelState.Remove(nameof(topic));
        }

        if (!IsDefined(topic))
        {
            return RedirectToAction(nameof(Index));
        }

        var page = await _bookingService.GetRequestPageAsync(userId, slotId, topic!.Value, info?.AppointmentId);
        if (page is null)
        {
            return NotFound();
        }

        // Which baby: the one of the appointment being rescheduled, else her only baby, else she picks.
        int? babyId = null;
        if (BookingService.IsBabyTopic(topic.Value))
        {
            babyId = info?.BabyId is { } oldBaby && page.Babies.Any(b => b.Id == oldBaby) ? oldBaby
                : page.Babies.Count == 1 ? page.Babies[0].Id
                : null;
        }

        return View(new BookRequestViewModel
        {
            SlotId = slotId,
            Topic = topic,
            BabyId = babyId,
            Concern = info?.Concern,
            RescheduleId = info?.AppointmentId,
            Reschedule = info,
            Page = page
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Request")]
    public async Task<IActionResult> SendRequest(BookRequestViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        if (model.RescheduleId is not null)
        {
            model.Reschedule = await _bookingService.GetRescheduleAsync(userId, model.RescheduleId.Value);
            if (model.Reschedule is null)
            {
                return CantChange();
            }

            model.Topic = model.Reschedule.Topic;
            ModelState.Remove(nameof(model.Topic));
        }

        if (!IsDefined(model.Topic))
        {
            return RedirectToAction(nameof(Index));
        }

        var page = await _bookingService.GetRequestPageAsync(userId, model.SlotId, model.Topic!.Value, model.RescheduleId);
        if (page is null)
        {
            return NotFound();
        }

        // Baby topics: she says which of her babies the consultation is about.
        if (BookingService.IsBabyTopic(model.Topic.Value) && page.Babies.Count > 0 && model.BabyId is null)
        {
            ModelState.AddModelError(nameof(model.BabyId), _localizer["Book_WhichBabyRequired"]);
        }

        if (ModelState.IsValid)
        {
            var result = await _bookingService.RequestAsync(
                userId,
                new BookingRequest(
                    model.SlotId,
                    model.Topic.Value,
                    model.Concern!,
                    model.ShareCheckIns,
                    model.BabyId,
                    model.ShareBaby,
                    model.RescheduleId));

            if (result.Status == BookingStatus.Sent)
            {
                TempData[StatusMessageKey] = _localizer[
                    "Book_Sent",
                    result.ProfessionalName!,
                    BookText.Moment(_localizer, result.ConfirmBy!.Value)].Value;
                return RedirectToAction(nameof(AppointmentsController.Index), "Appointments");
            }

            if (result.Status == BookingStatus.RescheduleGone)
            {
                return CantChange();
            }

            if (result.Status == BookingStatus.Invalid)
            {
                return NotFound();
            }

            model.Problem = result.Status;
        }

        model.Page = page;

        return View(model);
    }

    // The appointment being rescheduled can no longer be changed: back to My appointments with a message.
    private RedirectToActionResult CantChange()
    {
        TempData[ProblemMessageKey] = _localizer["Book_Problem_RescheduleGone"].Value;
        return RedirectToAction(nameof(AppointmentsController.Index), "Appointments");
    }

    private static List<FreeSlotDay> Split(IReadOnlyList<FreeSlotDay> days, Func<FreeSlot, bool> keep) =>
        days.Select(d => new FreeSlotDay(d.Date, d.Slots.Where(keep).ToList()))
            .Where(d => d.Slots.Count > 0)
            .ToList();

    private static bool IsDefined(Topic? topic) => topic is not null && Enum.IsDefined(topic.Value);
}
