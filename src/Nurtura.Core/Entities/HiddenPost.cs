namespace Nurtura.Core.Entities;

// A forum post a mother hid from her own view. At most once per mother and post.
public class HiddenPost
{
    public int Id { get; set; }

    public int MotherProfileId { get; set; }

    public int ForumPostId { get; set; }

    // UTC.
    public DateTime HiddenAt { get; set; }
}
