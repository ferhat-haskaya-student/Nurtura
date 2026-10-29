namespace Nurtura.Core.Entities;

// Age is never stored; it is always calculated from BirthDate.
public class Baby
{
    public const int NameMaxLength = 50;

    public const int BirthWeekMin = 22;
    public const int BirthWeekMax = 44;
    public const int BirthWeekDefault = 40;

    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly BirthDate { get; set; }

    public BirthType BirthType { get; set; }

    // Week of pregnancy at birth. Only used so age-based guides can allow for an early birth.
    public int BirthWeek { get; set; }

    // How the baby is fed now.
    public BabyFeedingType FeedingType { get; set; }

    // Not used yet; photo upload comes later.
    public string? PhotoUrl { get; set; }
}
