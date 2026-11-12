using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Admin.Controllers;

// The admin's Professionals page: the approval list, approved and deactivated professionals, deactivate /
// reactivate, and the topics each one covers. Admin role only, on every action.
[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class ProfessionalsController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string ProblemMessageKey = "ProblemMessage";

    private readonly ProfessionalApprovalService _approvalService;
    private readonly ProfessionalAdminService _adminService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProfessionalsController(
        ProfessionalApprovalService approvalService,
        ProfessionalAdminService adminService,
        IStringLocalizer<SharedResource> localizer)
    {
        _approvalService = approvalService;
        _adminService = adminService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await _adminService.GetOverviewAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        // Null means she was not Pending any more; nothing changed, so there is nothing to confirm.
        var name = await _approvalService.ApproveAsync(id);
        if (name is not null)
        {
            TempData[StatusMessageKey] = _localizer["Admin_Approved", name].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var name = await _approvalService.RejectAsync(id);
        if (name is not null)
        {
            TempData[StatusMessageKey] = _localizer["Admin_Rejected", name].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    // The confirmation page: what deactivating her cancels, or why it cannot be done now.
    [HttpGet]
    public async Task<IActionResult> Deactivate(int id)
    {
        var info = await _adminService.GetDeactivationAsync(id);
        if (info is null)
        {
            return NotFound();
        }

        return View(info);
    }

    // The service checks everything again inside its transaction.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Deactivate))]
    public async Task<IActionResult> DeactivateConfirmed(int id)
    {
        var result = await _adminService.DeactivateAsync(id);
        switch (result.Status)
        {
            case DeactivateStatus.Deactivated:
                TempData[StatusMessageKey] = _localizer[
                    Plural.Key("AdminPro_Deactivated", result.CancelledCount), result.DisplayName, result.CancelledCount].Value;
                break;
            case DeactivateStatus.SessionInProgress:
                TempData[ProblemMessageKey] = _localizer["AdminPro_SessionInProgress", result.DisplayName].Value;
                break;
            default:
                return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        var name = await _adminService.ReactivateAsync(id);
        if (name is null)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["AdminPro_Reactivated", name].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Topics(int id)
    {
        var edit = await _adminService.GetTopicsAsync(id);
        if (edit is null)
        {
            return NotFound();
        }

        return View(edit);
    }

    // At least one topic; the service checks again. A value that is not one of the six fails binding (ModelState),
    // so it is refused instead of being silently left out.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Topics(int id, List<Topic> topics)
    {
        if (!ModelState.IsValid || topics.Count == 0 || topics.Any(t => !Enum.IsDefined(t)))
        {
            var current = await _adminService.GetTopicsAsync(id);
            if (current is null)
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, _localizer["Validation_SelectAtLeastOne"]);
            return View(current with { Topics = topics });
        }

        var name = await _adminService.SetTopicsAsync(id, topics);
        if (name is null)
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["AdminPro_TopicsSaved", name].Value;
        return RedirectToAction(nameof(Index));
    }
}
