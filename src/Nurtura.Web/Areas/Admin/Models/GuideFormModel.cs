using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Admin.Models;

// The guide form for New and Edit. Every rule is checked by GuidanceAdminService on the server; the form only
// carries the values. Names and messages are keys in Resources/en.json and pl.json.
public class GuideFormModel
{
    // 0 for a new page.
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? Body { get; set; }

    public GuidanceAudience? Audience { get; set; }

    public int? CategoryId { get; set; }

    public Topic? RelatedTopic { get; set; }

    public int? BabyAgeFromMonths { get; set; }

    public int? BabyAgeToMonths { get; set; }

    public string? GetHelpHeading { get; set; }

    // One item per line.
    public string? GetHelpItems { get; set; }

    public string? EmergencyHeading { get; set; }

    // One item per line.
    public string? EmergencyItems { get; set; }

    // One source per line: "name | URL".
    public string? Sources { get; set; }

    // Comma separated.
    public string? Keywords { get; set; }

    public Language? Language { get; set; }

    public bool IsPublished { get; set; }

    // Filled in by the controller for display; not posted.
    public string? Slug { get; set; }

    public DateOnly? LastChecked { get; set; }

    public DateOnly? NextCheck { get; set; }

    public IReadOnlyList<GuidanceCategory> Categories { get; set; } = [];

    // The body rendered by the Guides renderer, after Preview.
    public string? PreviewHtml { get; set; }

    public GuideInput ToInput() => new(
        Title, Summary, Body, Audience, CategoryId, RelatedTopic, BabyAgeFromMonths, BabyAgeToMonths,
        GetHelpHeading, GetHelpItems, EmergencyHeading, EmergencyItems, Sources, Keywords, Language, IsPublished);

    public static GuideFormModel From(GuideEdit edit)
    {
        var input = edit.Input;
        return new GuideFormModel
        {
            Id = edit.Id,
            Slug = edit.Slug,
            LastChecked = edit.LastChecked,
            NextCheck = edit.NextCheck,
            Title = input.Title,
            Summary = input.Summary,
            Body = input.Body,
            Audience = input.Audience,
            CategoryId = input.CategoryId,
            RelatedTopic = input.RelatedTopic,
            BabyAgeFromMonths = input.BabyAgeFromMonths,
            BabyAgeToMonths = input.BabyAgeToMonths,
            GetHelpHeading = input.GetHelpHeading,
            GetHelpItems = input.GetHelpItems,
            EmergencyHeading = input.EmergencyHeading,
            EmergencyItems = input.EmergencyItems,
            Sources = input.Sources,
            Keywords = input.Keywords,
            Language = input.Language,
            IsPublished = input.IsPublished
        };
    }
}
