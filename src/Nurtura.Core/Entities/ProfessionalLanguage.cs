namespace Nurtura.Core.Entities;

// One row per language the professional speaks.
public class ProfessionalLanguage
{
    public int Id { get; set; }

    public int ProfessionalProfileId { get; set; }

    public Language Language { get; set; }
}
