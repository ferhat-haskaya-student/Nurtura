using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The baby questions, the same in onboarding and when she adds or edits a baby.
public sealed record BabyDetails(
    string Name,
    DateOnly BirthDate,
    BirthType BirthType,
    int BirthWeek,
    BabyFeedingType FeedingType)
{
    // The forms check these first; this is the last line of defence.
    internal void EnsureValid(DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > Baby.NameMaxLength)
        {
            throw new ArgumentException("The baby's name is empty or too long.");
        }

        if (BirthDate > today)
        {
            throw new ArgumentException("The birth date cannot be in the future.");
        }

        if (BirthWeek is < Baby.BirthWeekMin or > Baby.BirthWeekMax)
        {
            throw new ArgumentException("The birth week is out of range.");
        }

        if (!Enum.IsDefined(BirthType))
        {
            throw new ArgumentException("The birth type is not one of the options.");
        }

        if (!Enum.IsDefined(FeedingType))
        {
            throw new ArgumentException("The feeding type is not one of the options.");
        }
    }

    internal void CopyTo(Baby baby)
    {
        baby.Name = Name.Trim();
        baby.BirthDate = BirthDate;
        baby.BirthType = BirthType;
        baby.BirthWeek = BirthWeek;
        baby.FeedingType = FeedingType;
    }
}
