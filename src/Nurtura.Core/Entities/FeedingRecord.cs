namespace Nurtura.Core.Entities;

// One feed of one baby, logged after it. Only the chosen type's fields are filled; the rest stay empty
// (checked in FeedingDetails and by the database). Minutes are never turned into ml.
public class FeedingRecord
{
    public const int MinutesMin = 1;
    public const int MinutesMax = 120;
    public const int AmountMlMin = 1;
    public const int AmountMlMax = 500;
    public const int NoteMaxLength = 500;

    // Tap buttons for breastfeed minutes; any other value can be typed.
    public static readonly int[] QuickMinutes = [5, 10, 15, 20, 30];

    public int Id { get; set; }

    public int BabyId { get; set; }

    // UTC, cut to whole minutes.
    public DateTime Time { get; set; }

    public FeedingType Type { get; set; }

    // Breast only; required for a breastfeed.
    public BreastSide? BreastSide { get; set; }

    // Breast only; optional.
    public int? Minutes { get; set; }

    // Bottle only; optional.
    public int? AmountMl { get; set; }

    // Bottle only; required for a bottle feed.
    public BottleContent? BottleContent { get; set; }

    public string? Note { get; set; }

    // Private by default: only a professional she books can see a shared feed.
    public bool IsShared { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC.
    public DateTime UpdatedAt { get; set; }
}
