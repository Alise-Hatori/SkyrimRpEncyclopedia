namespace SkyrimRpEncyclopedia.Models;

public sealed class OverlaySettings
{
    public string SelectedMapId { get; set; } = MapProviders.NordCore;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 460;
    public double Height { get; set; } = 520;
    public double Opacity { get; set; } = 0.90;
    public bool IsMapVisible { get; set; }
    public double MapHeight { get; set; } = 220;
    public bool IsMiniMapVisible { get; set; }
    public double? MiniMapLeft { get; set; }
    public double? MiniMapTop { get; set; }
    public double MiniMapDiameter { get; set; } = 280;
    public double MiniMapOpacity { get; set; } = 0.95;
    public string Theme { get; set; } = OverlayAppearance.NordicFrost;
    public double FontSize { get; set; } = 14;
    public bool ShowDecorativeBorder { get; set; } = true;
    public bool ShowArticleTitle { get; set; } = true;
    public bool ShowArticleCategory { get; set; } = true;
    public bool ShowArticleContent { get; set; } = true;
    public bool ShowRelatedArticles { get; set; } = true;
    public bool ShowEmbeddedMap { get; set; } = true;
    public string LayoutMode { get; set; } = OverlayLayoutModes.Normal;
    public bool IsCompactMode { get; set; }
    public bool IsPositionLocked { get; set; }
}

public static class OverlayLayoutModes
{
    public const string Normal = "Обычный";
    public const string Compact = "Компактный";
    public const string ArticleOnly = "Только статья";

    public static IReadOnlyList<string> All { get; } =
        new[] { Normal, Compact, ArticleOnly };

    public static bool IsSupported(string? layoutMode) => All.Contains(layoutMode);
}
