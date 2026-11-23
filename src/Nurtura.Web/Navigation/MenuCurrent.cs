using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nurtura.Web.Navigation;

// The aria-current value of a menu link: "page" on the link's own page, "true" on another page of its section,
// null (no attribute) everywhere else. site.css styles the link from the same attribute.
public static class MenuCurrent
{
    // A section: every action of the controller, and of the other controllers that belong to it.
    public static string? Section(ViewContext context, string area, string controller, params string[] otherControllers)
    {
        if (!Is(context, "area", area))
        {
            return null;
        }

        if (Is(context, "controller", controller))
        {
            return Is(context, "action", "Index") ? "page" : "true";
        }

        return otherControllers.Any(other => Is(context, "controller", other)) ? "true" : null;
    }

    // One single page.
    public static string? Page(ViewContext context, string area, string controller, string action) =>
        Is(context, "area", area) && Is(context, "controller", controller) && Is(context, "action", action) ? "page" : null;

    // The action's own names, not the address, so the case of a typed URL does not matter.
    private static bool Is(ViewContext context, string key, string value)
    {
        context.ActionDescriptor.RouteValues.TryGetValue(key, out var current);
        return string.Equals(current ?? "", value, StringComparison.OrdinalIgnoreCase);
    }
}
