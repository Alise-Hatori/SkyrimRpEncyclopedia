namespace SkyrimRpEncyclopedia.Models;

public class ArticleSource
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public bool ArticleIsOfficial { get; set; }

    public string Title { get; set; } = "";

    public string Url { get; set; } = "";

    public string SourceType { get; set; } = "Website";

    public string Author { get; set; } = "";

    public string Description { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}