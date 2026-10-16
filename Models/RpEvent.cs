namespace SkyrimRpEncyclopedia.Models;

public class RpEvent
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string EventType { get; set; } = "";

    public string Location { get; set; } = "";

    public string Faction { get; set; } = "";

    public string Characters { get; set; } = "";

    public string Objective { get; set; } = "";

    public string Conditions { get; set; } = "";

    public string Reward { get; set; } = "";

    public string Extra { get; set; } = "";

    public string GeneratedText { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}