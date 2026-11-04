using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// The full routine summary page of one of her babies.
public sealed record RoutineViewModel(Baby Baby, BabyRoutine Routine);
