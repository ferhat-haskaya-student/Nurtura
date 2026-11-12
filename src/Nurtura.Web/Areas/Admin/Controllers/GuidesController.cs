using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Areas.Admin.Models;
using Nurtura.Web.Content;

namespace Nurtura.Web.Areas.Admin.Controllers;

// The admin's Guides editor: all pages (published or not), one form for New and Edit with Preview, and "Mark as
// checked today". Admin role only, on every action. GuidanceAdminService checks every rule on the server.
[Area(Roles.Admin)]
[Authorize(Roles = Roles.Admin)]
public class GuidesController : Controller
{
    private const string StatusMessageKey = "StatusMessage";
    private const string FormView = "Form";

    private readonly GuidanceAdminService _guidanceService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GuidesController(GuidanceAdminService guidanceService, IStringLocalizer<SharedResource> localizer)
    {
        _guidanceService = guidanceService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await _guidanceService.GetListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> New()
    {
        return await Form(new GuideFormModel { Language = Language.English });
    }

    // preview: the Preview button was used; the body is rendered and nothing is saved.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(GuideFormModel model, string? preview)
    {
        model.Id = 0;
        if (preview is not null || !TakeBindingErrors(model))
        {
            return await Form(model, preview is not null);
        }

        var result = await _guidanceService.CreateAsync(model.ToInput());
        if (!result.Saved)
        {
            AddErrors(result.Errors);
            return await Form(model);
        }

        TempData[StatusMessageKey] = _localizer["GuideAdmin_Created"].Value;
        return RedirectToAction(nameof(Edit), new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await _guidanceService.GetAsync(id);
        if (page is null)
        {
            return NotFound();
        }

        return await Form(GuideFormModel.From(page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, GuideFormModel model, string? preview)
    {
        var current = await _guidanceService.GetAsync(id);
        if (current is null)
        {
            return NotFound();
        }

        // Shown, never posted back: the slug and the check dates come from the database.
        model.Id = id;
        model.Slug = current.Slug;
        model.LastChecked = current.LastChecked;
        model.NextCheck = current.NextCheck;
        if (preview is not null || !TakeBindingErrors(model))
        {
            return await Form(model, preview is not null);
        }

        var result = await _guidanceService.UpdateAsync(id, model.ToInput());
        if (!result.Found)
        {
            return NotFound();
        }

        if (!result.Saved)
        {
            AddErrors(result.Errors);
            return await Form(model);
        }

        TempData[StatusMessageKey] = _localizer["GuideAdmin_Saved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Last checked = today (Poland time), next check = a year later.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkChecked(int id)
    {
        if (!await _guidanceService.MarkCheckedAsync(id))
        {
            return NotFound();
        }

        TempData[StatusMessageKey] = _localizer["GuideAdmin_MarkedChecked"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<IActionResult> Form(GuideFormModel model, bool preview = false)
    {
        model.Categories = await _guidanceService.GetCategoriesAsync();
        if (preview)
        {
            // The same safe renderer as the Guides pages: raw HTML off, unsafe link addresses removed.
            model.PreviewHtml = GuidanceMarkdown.ToHtml(model.Body ?? string.Empty);
        }

        return View(FormView, model);
    }

    // A value MVC could not read (letters in a number, an unknown option) would otherwise arrive as empty and could
    // pass as "not filled in". Each such field gets one plain message instead, and nothing is saved.
    // Returns true when there were none.
    private bool TakeBindingErrors(GuideFormModel model)
    {
        var bad = ModelState.Where(e => e.Value?.Errors.Count > 0).Select(e => e.Key).ToList();
        ModelState.Clear();
        foreach (var field in bad)
        {
            ModelState.AddModelError(field, _localizer["GuideAdmin_Error_Invalid"]);
        }

        return bad.Count == 0;
    }

    private void AddErrors(IEnumerable<GuideError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Field, _localizer[error.Key, error.Args]);
        }
    }
}
