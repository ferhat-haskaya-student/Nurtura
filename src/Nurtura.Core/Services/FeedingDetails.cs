using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// What the feeding screen sends. The time is UTC. Only the chosen type's fields may be filled:
// breast needs a side (minutes optional), a bottle needs its content (ml optional), solids nothing.
public sealed record FeedingDetails(
    DateTime TimeUtc,
    FeedingType Type,
    BreastSide? BreastSide,
    int? Minutes,
    int? AmountMl,
    BottleContent? BottleContent,
    string? Note,
    bool IsShared)
{
    // CRLF counts as one character, like the counter under the box. Empty means no note.
    internal string? NormalizedNote =>
        string.IsNullOrWhiteSpace(Note) ? null : Note.ReplaceLineEndings("\n").Trim();

    // The form checks these first; this is the last line of defence. The database checks them too.
    internal void EnsureValid()
    {
        if (!Enum.IsDefined(Type))
        {
            throw new ArgumentException("The feeding type is not one of the options.");
        }

        var (sideOk, minutesOk, amountOk, contentOk) = Type switch
        {
            FeedingType.Breast => (
                BreastSide is { } side && Enum.IsDefined(side),
                Minutes is null or (>= FeedingRecord.MinutesMin and <= FeedingRecord.MinutesMax),
                AmountMl is null,
                BottleContent is null),
            FeedingType.Bottle => (
                BreastSide is null,
                Minutes is null,
                AmountMl is null or (>= FeedingRecord.AmountMlMin and <= FeedingRecord.AmountMlMax),
                BottleContent is { } content && Enum.IsDefined(content)),
            _ => (BreastSide is null, Minutes is null, AmountMl is null, BottleContent is null)
        };

        if (!(sideOk && minutesOk && amountOk && contentOk))
        {
            throw new ArgumentException($"The fields do not fit a {Type} feed.");
        }

        if (NormalizedNote?.Length > FeedingRecord.NoteMaxLength)
        {
            throw new ArgumentException("The note is too long.");
        }
    }

    internal void CopyTo(FeedingRecord feed)
    {
        feed.Time = SleepService.ToMinute(TimeUtc);
        feed.Type = Type;
        feed.BreastSide = BreastSide;
        feed.Minutes = Minutes;
        feed.AmountMl = AmountMl;
        feed.BottleContent = BottleContent;
        feed.Note = NormalizedNote;
        feed.IsShared = IsShared;
    }
}

public enum FeedingSaveStatus
{
    Saved,
    NotFound,
    InFuture
}

public sealed record FeedingSaveResult(FeedingSaveStatus Status, int? Id = null);

// What the feeding screen starts with: the type to pre-select, the bottle content to pre-select,
// and the side of the last breastfeed (shown as a hint only, never pre-selected).
public sealed record FeedingDefaults(FeedingType Type, BottleContent? LastBottleContent, BreastSide? LastBreastSide);
