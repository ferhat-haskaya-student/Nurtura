namespace Nurtura.Core.Entities;

// One row per concern the mother ticked during onboarding.
public class MotherConcern
{
    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public Topic Concern { get; set; }
}
