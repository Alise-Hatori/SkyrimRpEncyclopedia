namespace SkyrimRpEncyclopedia.Models;

public class ArticleNote
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public bool ArticleIsOfficial { get; set; }

    public string NoteType { get; set; } = "Private";

    public string Content { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
