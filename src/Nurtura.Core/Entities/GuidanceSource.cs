namespace Nurtura.Core.Entities;

// A named public source of a guidance page, shown to the mother as a link. One or more per page.
public class GuidanceSource
{
    public const int NameMaxLength = 300;
    public const int UrlMaxLength = 500;

    public int Id { get; set; }

    public int GuidanceContentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    // Shown in this order.
    public int SortOrder { get; set; }
}
