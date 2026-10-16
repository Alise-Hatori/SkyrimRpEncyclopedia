namespace SkyrimRpEncyclopedia.Models;

public class ImportedArticle
{
    public string Title { get; set; } = "";

    public string Category { get; set; } = "";

    public string Summary { get; set; } = "";

    public string Content { get; set; } = "";

    public List<string> Tags { get; set; } = new();

    public string Flags { get; set; } = "";

    public bool IsOfficial { get; set; }

    public string SourceTitle { get; set; } = "";

    public string SourceUrl { get; set; } = "";

    public string SourceType { get; set; } = "Website";

    public List<ImportedRelation> Relations { get; set; } = new();
}

public class ImportedRelation
{
    public string TargetTitle { get; set; } = "";

    public string LinkType { get; set; } = "";

    public string Description { get; set; } = "";
}