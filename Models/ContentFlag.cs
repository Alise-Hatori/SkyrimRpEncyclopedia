namespace SkyrimRpEncyclopedia.Models;

public class ContentFlag
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Icon { get; set; } = "";

    public string Color { get; set; } = "";

    public string Description { get; set; } = "";

    public bool IsSystem { get; set; }

    public int UsageCount { get; set; }

    // Resource IDs are rendered as vector images by the metadata catalog, not as raw text in legacy pickers.
    public string DisplayName => Icon?.StartsWith("metadata:", StringComparison.Ordinal) == true
        ? $"{Name} · {UsageCount}" : $"{Icon} {Name} · {UsageCount}";
}
