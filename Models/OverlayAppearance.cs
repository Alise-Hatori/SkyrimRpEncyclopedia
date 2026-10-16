namespace SkyrimRpEncyclopedia.Models;

public static class OverlayAppearance
{
    public const string NordicFrost = "Nordic Frost";
    public const string ImperialArchive = "Imperial Archive";
    public const string Stealth = "Stealth";
    public const string FrostMagic = "Frost Magic";

    public static IReadOnlyList<string> Themes { get; } =
        new[] { NordicFrost, ImperialArchive, Stealth, FrostMagic };

    public static bool IsSupportedTheme(string? theme) => Themes.Contains(theme);

    public static OverlayThemePalette GetPalette(string? theme) => theme switch
    {
        ImperialArchive => new("#E6231E17", "#8B7355", "#E9DFC9", "#B9A589", "#D8D0C1", "#B51A1712", "#332E271D", "#7A6449"),
        Stealth => new("#E6111315", "#586166", "#E7EAEB", "#A5ACAF", "#D2D6D8", "#A5101213", "#2B25292B", "#6A7479"),
        FrostMagic => new("#E615222A", "#87AAB5", "#E7F0F2", "#ACC6CD", "#D5E4E7", "#B7111C23", "#2D28424B", "#91C2CC"),
        _ => new("#E61B1E20", "#7F9098", "#E7EAEB", "#AFBBC0", "#D6DCDE", "#B515181A", "#2B293236", "#93AAB4")
    };
}

public sealed record OverlayThemePalette(
    string Background,
    string Border,
    string PrimaryText,
    string SecondaryText,
    string ContentText,
    string Surface,
    string Hover,
    string Accent);
