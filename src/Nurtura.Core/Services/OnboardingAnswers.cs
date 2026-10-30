using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Everything the mother answers on the onboarding form.
public sealed record OnboardingAnswers(
    IReadOnlyCollection<Topic> Concerns,
    IReadOnlyCollection<SupportType> SupportTypes,
    bool IsFirstBaby,
    HelpAtHome HelpAtHome,
    string BabyName,
    DateOnly BabyBirthDate,
    BirthType BirthType,
    int BirthWeek,
    BabyFeedingType FeedingType);
