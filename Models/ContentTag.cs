namespace SkyrimRpEncyclopedia.Models;

public class ContentTag
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public int UsageCount { get; set; }

    public string DisplayName => $"#{Name} · {UsageCount}";
}
