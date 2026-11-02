namespace Nurtura.Core.Services;

// Everything the mother fills in on the daily check-in form.
public sealed record CheckInAnswers(
    DateOnly Date,
    int Mood,
    int SleepQuality,
    int Energy,
    int Stress,
    IReadOnlyCollection<int> FeelingTagIds,
    string? Note,
    bool IsShared);
