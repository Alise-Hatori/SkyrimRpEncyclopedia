namespace SkyrimRpEncyclopedia.Models;

public class ContentCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public bool IsSystem { get; set; }

    public int SortOrder { get; set; }
}
