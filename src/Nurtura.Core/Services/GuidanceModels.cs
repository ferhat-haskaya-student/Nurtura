using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// A guidance page as a card in the lists: title and one-line summary.
public sealed record GuidanceCard(int Id, string Title, string Summary);

// The cards of one category inside "For you" or "For your baby".
public sealed record GuidanceGroup(string Category, IReadOnlyList<GuidanceCard> Cards);

public sealed record GuidanceList(
    IReadOnlyList<GuidanceCard> Suggested,
    IReadOnlyList<GuidanceGroup> ForYou,
    IReadOnlyList<GuidanceGroup> ForYourBaby);

// One page with its category and sources (in order), and whether she saved it.
public sealed record GuidancePage(GuidanceContent Content, bool IsSaved);
