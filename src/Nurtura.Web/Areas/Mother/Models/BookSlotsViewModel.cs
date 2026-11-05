using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// The free times page. When rescheduling, the same professional's times come first (SameProfessional)
// and Days holds everyone else's; otherwise SameProfessional is empty and Days holds all free times.
public class BookSlotsViewModel
{
    public Topic Topic { get; init; }

    public Language? Language { get; init; }

    public RescheduleInfo? Reschedule { get; init; }

    public IReadOnlyList<FreeSlotDay> SameProfessional { get; init; } = [];

    public IReadOnlyList<FreeSlotDay> Days { get; init; } = [];
}
