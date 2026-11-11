namespace Nurtura.Core.Entities;

// A forum post a mother saved for later. At most once per mother and post.
public class SavedPost
{
    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public int ForumPostId { get; set; }

    // UTC.
    public DateTime SavedAt { get; set; }
}
