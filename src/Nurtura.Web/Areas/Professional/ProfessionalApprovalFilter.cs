using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Professional;

// Registered globally: until a professional is approved, every page in the Professional area
// except the approval page itself sends her to the approval page.
public class ProfessionalApprovalFilter : IAsyncActionFilter
{
    private const string ApprovalController = "Approval";

    private readonly ProfessionalApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfessionalApprovalFilter(ProfessionalApprovalService approvalService, UserManager<ApplicationUser> userManager)
    {
        _approvalService = approvalService;
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var routeValues = context.ActionDescriptor.RouteValues;
        routeValues.TryGetValue("area", out var area);
        routeValues.TryGetValue("controller", out var controller);
        var user = context.HttpContext.User;

        if (string.Equals(area, Roles.Professional, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(controller, ApprovalController, StringComparison.OrdinalIgnoreCase)
            && user.IsInRole(Roles.Professional)
            && await _approvalService.GetStatusAsync(_userManager.GetUserId(user)!) != ApprovalStatus.Approved)
        {
            context.Result = new RedirectToActionResult("Index", ApprovalController, new { area = Roles.Professional });
            return;
        }

        await next();
    }
}
