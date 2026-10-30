namespace Nurtura.Core.Entities;

// One row per topic the professional covers.
public class ProfessionalCategory
{
    public int Id { get; set; }

    public int ProfessionalProfileId { get; set; }

    public Topic Category { get; set; }
}
