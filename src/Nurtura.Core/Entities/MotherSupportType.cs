namespace Nurtura.Core.Entities;

// One row per support type the mother ticked during onboarding.
public class MotherSupportType
{
    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public SupportType SupportType { get; set; }
}
