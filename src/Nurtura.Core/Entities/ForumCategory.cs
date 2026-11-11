namespace Nurtura.Core.Entities;

// One of the nine fixed forum categories. The label shown is the resource key "ForumCategory_" + Key.
public class ForumCategory
{
    public const int KeyMaxLength = 30;

    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;
}
