namespace SkyrimRpEncyclopedia.Models;

public class ImportBatchResult
{
    public int Total { get; set; }

    public int Added { get; set; }

    public int Updated { get; set; }

    public int Failed { get; set; }

    public List<string> Errors { get; set; } = new();
}