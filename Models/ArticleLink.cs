namespace SkyrimRpEncyclopedia.Models;

public class ArticleLink
{
    public int Id { get; set; }

    public int SourceArticleId { get; set; }

    public bool SourceIsOfficial { get; set; }

    public int TargetArticleId { get; set; }

    public bool TargetIsOfficial { get; set; }

    public string LinkType { get; set; } = "";

    public string Description { get; set; } = "";
}