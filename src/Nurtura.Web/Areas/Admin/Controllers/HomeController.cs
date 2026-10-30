using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Admin.Controllers;

[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class HomeController : Controller
{
    private const string StatusMessageKey = "StatusMessage";

    private readonly ProfessionalApprovalService _approvalService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public HomeController(ProfessionalApprovalService approvalService, IStringLocalizer<SharedResource> localizer)
    {
        _approvalService = approvalService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await _approvalService.GetPendingAsync());
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
}
