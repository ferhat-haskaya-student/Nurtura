namespace Nurtura.Core.Entities;

// One of the eight fixed feeling tags. The label shown is the resource key "FeelingTag_" + Key.
public class FeelingTag
{
    public const int KeyMaxLength = 30;

    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;
}
