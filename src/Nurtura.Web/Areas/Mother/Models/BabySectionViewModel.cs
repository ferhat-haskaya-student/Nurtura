using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// The baby section: all her babies (for the selector) and the selected one's card.
// Selected and Age are null when she has no baby.
public sealed record BabySectionViewModel(IReadOnlyList<Baby> Babies, Baby? Selected, BabyAge? Age);
