namespace Nurtura.Core.Entities;

// The mother's own check-in, about her, not the baby. One per mother per date.
public class DailyCheckIn
{
    public const int RatingMin = 1;
    public const int RatingMax = 5;
    public const int NoteMaxLength = 500;

    // She can fill in or edit today and this many days before it.
    public const int EditableDays = 7;

    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public DateOnly Date { get; set; }

    // Ratings are 1 to 5. For mood, sleep quality and energy 5 is best; for stress 5 means more stress.
    public int Mood { get; set; }

    public int SleepQuality { get; set; }

    public int Energy { get; set; }

    public int Stress { get; set; }

    public string? Note { get; set; }

    // False (private) unless she chooses to share the whole check-in (ratings, tags and note)
    // with a professional she books.
    public bool IsShared { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC.
    public DateTime UpdatedAt { get; set; }

    public List<CheckInFeelingTag> FeelingTags { get; set; } = [];
}
