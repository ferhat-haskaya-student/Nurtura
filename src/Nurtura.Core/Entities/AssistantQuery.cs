namespace Nurtura.Core.Entities;

// One Nurtura Assistant search, kept only for counts (does the assistant work?).
// Deliberately no mother and no search text (GDPR data minimisation): nobody, admin included,
// can read what a mother typed.
public class AssistantQuery
{
    public int Id { get; set; }

    // Day of the search in Polish time; no time of day.
    public DateOnly Date { get; set; }

    // True when a page or a topic was found.
    public bool Matched { get; set; }

    public Topic? SuggestedTopic { get; set; }

    public bool UrgentShown { get; set; }
}
