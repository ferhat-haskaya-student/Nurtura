using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Mother.Models;

namespace Nurtura.Web.Areas.Mother.Controllers;

// The Nurtura Assistant results. The search is a POST and the results are shown directly (no redirect), so the
// text never appears in a URL: not in browser history, server logs or Referer headers. It is never stored
// or logged; AssistantService saves only an anonymous AssistantQuery row.
[Area(Roles.Mother)]
[Authorize(Roles = Roles.Mother)]
public class AssistantController : Controller
{
    public const string FromBook = "Book";
    public const string FromGuides = "Guides";

    private readonly AssistantService _assistantService;

    public AssistantController(AssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    // Opened without a search (refresh after leaving, a bookmark): back to Guides.
    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(GuidesController.Index), FromGuides);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string? search, string? from)
    {
        from = from == FromBook ? FromBook : FromGuides;
        var result = await _assistantService.SearchAsync(search);
        if (result is null)
        {
            return RedirectToAction("Index", from);
        }

        var box = new AssistantSearchBox(LabelKey(from), from, search!.Trim());
        return View(new AssistantResultsViewModel(box, result));
    }

    public static string LabelKey(string from) =>
        from == FromBook ? "Assistant_BookLabel" : "Assistant_GuidesLabel";
}
