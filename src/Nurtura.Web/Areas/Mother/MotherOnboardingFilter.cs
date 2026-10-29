using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother;

// Registered globally: until a mother finishes onboarding, every page in the Mother area
// except the onboarding form itself sends her to the form.
public class MotherOnboardingFilter : IAsyncActionFilter
{
    private const string OnboardingController = "Onboarding";

    private readonly MotherOnboardingService _onboardingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MotherOnboardingFilter(MotherOnboardingService onboardingService, UserManager<ApplicationUser> userManager)
    {
        _onboardingService = onboardingService;
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var routeValues = context.ActionDescriptor.RouteValues;
        routeValues.TryGetValue("area", out var area);
        routeValues.TryGetValue("controller", out var controller);
        var user = context.HttpContext.User;

        if (string.Equals(area, Roles.Mother, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(controller, OnboardingController, StringComparison.OrdinalIgnoreCase)
            && user.IsInRole(Roles.Mother)
            && await _onboardingService.IsCompletedAsync(_userManager.GetUserId(user)!) != true)
        {
            context.Result = new RedirectToActionResult("Index", OnboardingController, new { area = Roles.Mother });
            return;
        }

        await next();
    }
}
