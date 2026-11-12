using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// One page on the admin's Guides list.
public sealed record GuideListItem(
    int Id,
    string Title,
    GuidanceAudience Audience,
    string CategoryName,
    DateOnly LastChecked,
    DateOnly NextCheck,
    bool IsPublished);

// What the admin typed in the guide form. The list fields are raw text: items one per line, sources one per line
// as "name | URL", keywords comma separated. GuidanceAdminService parses and checks everything.
public sealed record GuideInput(
    string? Title,
    string? Summary,
    string? Body,
    GuidanceAudience? Audience,
    int? CategoryId,
    Topic? RelatedTopic,
    int? BabyAgeFromMonths,
    int? BabyAgeToMonths,
    string? GetHelpHeading,
    string? GetHelpItems,
    string? EmergencyHeading,
    string? EmergencyItems,
    string? Sources,
    string? Keywords,
    Language? Language,
    bool IsPublished);

// An existing page as the form shows it: the list fields turned back into text.
public sealed record GuideEdit(int Id, string Slug, DateOnly LastChecked, DateOnly NextCheck, GuideInput Input);

// One problem with the form. Field is the form field's name; Key is a resource key, used with Args.
public sealed record GuideError(string Field, string Key, params object[] Args);

// Errors is empty when the page was saved; Found is false when the page to edit does not exist.
public sealed record GuideSaveResult(int Id, IReadOnlyList<GuideError> Errors, bool Found = true)
{
    public bool Saved => Found && Errors.Count == 0;
}
