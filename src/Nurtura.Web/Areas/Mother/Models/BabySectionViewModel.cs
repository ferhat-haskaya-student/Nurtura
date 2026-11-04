using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// The baby section: all her babies (for the selector), the selected one's card, its Sleep and Feeding tiles,
// the routine summary card, and the "Last 7 days" timeline of sleeps and feeds.
// Selected, Age, Sleep and Routine are null when she has no baby.
public sealed record BabySectionViewModel(
    IReadOnlyList<Baby> Babies,
    Baby? Selected,
    BabyAge? Age,
    SleepOverview? Sleep,
    FeedingRecord? LastFeed,
    IReadOnlyList<TimelineDay> Timeline,
    BabyRoutine? Routine);
