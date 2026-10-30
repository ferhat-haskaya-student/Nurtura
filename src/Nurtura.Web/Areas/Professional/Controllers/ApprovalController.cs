using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Professional.Controllers;

[Area(Roles.Professional)]
[Authorize(Roles = Roles.Professional)]
public class ApprovalController : Controller
{
    private readonly ProfessionalApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApprovalController(ProfessionalApprovalService approvalService, UserManager<ApplicationUser> userManager)
    {
        _approvalService = approvalService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await _approvalService.GetStatusAsync(_userManager.GetUserId(User)!) switch
        {
            ApprovalStatus.Pending => View("Pending"),
            ApprovalStatus.Rejected => View("Rejected"),
            ApprovalStatus.Approved => RedirectToAction("Index", "Home", new { area = Roles.Professional }),
            _ => NotFound()
        };
    }
}
