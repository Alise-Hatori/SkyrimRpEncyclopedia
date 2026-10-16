using System.Text.Json;
using System.IO;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public sealed class OverlaySettingsService
{
    private readonly string _path;

    public OverlaySettingsService(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyrimRpEncyclopedia",
            "overlay-settings.json");
    }

    public OverlaySettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new OverlaySettings();

            var settings = JsonSerializer.Deserialize<OverlaySettings>(File.ReadAllText(_path));
            return Normalize(settings ?? new OverlaySettings());
        }
        catch (JsonException)
        {
            return new OverlaySettings();
        }
        catch (IOException)
        {
            return new OverlaySettings();
        }
    }

    public void Save(OverlaySettings settings)
    {
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_path, JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static OverlaySettings Normalize(OverlaySettings settings)
    {
        return new OverlaySettings
        {
            SelectedMapId = MapProviders.Resolve(settings.SelectedMapId).Id,
            Left = IsFinite(settings.Left) ? settings.Left : null,
            Top = IsFinite(settings.Top) ? settings.Top : null,
            Width = Clamp(settings.Width, 320, 1000, 460),
            Height = Clamp(settings.Height, 240, 900, 520),
            Opacity = Clamp(settings.Opacity, 0.35, 1.0, 0.90),
            IsMapVisible = settings.IsMapVisible,
            MapHeight = Clamp(settings.MapHeight, 130, 360, 220),
            IsMiniMapVisible = settings.IsMiniMapVisible,
            MiniMapLeft = IsFinite(settings.MiniMapLeft) ? settings.MiniMapLeft : null,
            MiniMapTop = IsFinite(settings.MiniMapTop) ? settings.MiniMapTop : null,
            MiniMapDiameter = Clamp(settings.MiniMapDiameter, 180, 480, 280),
            MiniMapOpacity = Clamp(settings.MiniMapOpacity, 0.35, 1.0, 0.95),
            Theme = OverlayAppearance.IsSupportedTheme(settings.Theme)
                ? settings.Theme
                : OverlayAppearance.NordicFrost,
            FontSize = Clamp(settings.FontSize, 11, 22, 14),
            ShowDecorativeBorder = settings.ShowDecorativeBorder,
            ShowArticleTitle = settings.ShowArticleTitle,
            ShowArticleCategory = settings.ShowArticleCategory,
            ShowArticleContent = settings.ShowArticleContent,
            ShowRelatedArticles = settings.ShowRelatedArticles,
            ShowEmbeddedMap = settings.ShowEmbeddedMap,
            LayoutMode = OverlayLayoutModes.IsSupported(settings.LayoutMode)
                ? settings.LayoutMode
                : OverlayLayoutModes.Normal,
            IsCompactMode = settings.IsCompactMode,
            IsPositionLocked = settings.IsPositionLocked
        };
    }

    private static bool IsFinite(double? value) => value.HasValue && double.IsFinite(value.Value);

    private static double Clamp(double value, double minimum, double maximum, double fallback)
    {
        return double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }
}
