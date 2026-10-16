namespace SkyrimRpEncyclopedia.Models;

public sealed record MapProvider(string Id, string Name, Uri Address);

/// <summary>Stable settings IDs; never navigate to an address supplied by settings.</summary>
public static class MapProviders
{
    public const string NordCore = "nordcore";
    public const string SkyRp = "skyrp";
    public static IReadOnlyList<MapProvider> All { get; } = Array.AsReadOnly(new[]
    {
        new MapProvider(NordCore, "NordCore", new Uri("https://map.nordcore.dev/")),
        new MapProvider(SkyRp, "SkyRP Map", new Uri("https://skyrpmap.com/"))
    });

    public static MapProvider Resolve(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}
