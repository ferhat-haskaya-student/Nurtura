namespace Nurtura.Core.Entities;

// A group of guidance pages inside "For you" or "For your baby". The name is content, like the pages.
public class GuidanceCategory
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
