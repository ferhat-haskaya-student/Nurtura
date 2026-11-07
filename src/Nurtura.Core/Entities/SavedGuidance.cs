namespace Nurtura.Core.Entities;

// A guidance page a mother saved for later. At most once per mother and page.
public class SavedGuidance
{
    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public int GuidanceContentId { get; set; }

    // UTC.
    public DateTime SavedAt { get; set; }
}
