namespace Nurtura.Core.Entities;

// The type of one logged feed, chosen every time. Not the baby's usual feeding (BabyFeedingType). Stored as text.
// All three are always offered: the app never hides solids by age.
public enum FeedingType
{
    Breast,
    Bottle,
    Solid
}
