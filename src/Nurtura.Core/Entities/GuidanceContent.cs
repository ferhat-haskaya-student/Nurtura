namespace Nurtura.Core.Entities;

// One guidance page. Seeded first, edited through the admin panel later.
public class GuidanceContent
{
    public const int SlugMaxLength = 100;
    public const int TitleMaxLength = 200;
    public const int SummaryMaxLength = 500;
    public const int HeadingMaxLength = 200;

    public int Id { get; set; }

    // Fixed name of the page, never shown and never changed by renaming. The seeder recognises pages by it.
    public string Slug { get; set; } = string.Empty;

    public int GuidanceCategoryId { get; set; }

    public GuidanceAudience Audience { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    // Simple Markdown, rendered with raw HTML switched off.
    public string Body { get; set; } = string.Empty;

    // The appointment topic "Book a consultation" opens.
    public Topic RelatedTopic { get; set; }

    // Only for the baby nutrition stages: from (inclusive) to (exclusive), in full months of the baby's age.
    public int? BabyAgeFromMonths { get; set; }

    public int? BabyAgeToMonths { get; set; }

    // The blue "When to get help" card, on every page. The heading matches the urgency of the source.
    public string GetHelpHeading { get; set; } = string.Empty;

    public List<string> GetHelpItems { get; set; } = [];

    // The red emergency card, only where the topic needs it: no items means no card.
    public string? EmergencyHeading { get; set; }

    public List<string> EmergencyItems { get; set; } = [];

    public Language Language { get; set; }

    public DateOnly LastChecked { get; set; }

    public DateOnly NextCheck { get; set; }

    public bool IsPublished { get; set; }

    public GuidanceCategory? Category { get; set; }

    public List<GuidanceSource> Sources { get; set; } = [];

    public bool HasAgeRange => BabyAgeFromMonths is not null && BabyAgeToMonths is not null;
}
