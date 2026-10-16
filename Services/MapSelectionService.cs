using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>One choice for all map surfaces; reuses the existing settings file and browser profile.</summary>
public sealed class MapSelectionService
{
    private static readonly Lazy<MapSelectionService> SharedInstance = new(() => new(new OverlaySettingsService()));
    public static MapSelectionService Shared => SharedInstance.Value;
    private readonly OverlaySettingsService _settings;
    public MapProvider CurrentMap { get; private set; }
    public event Action<MapProvider>? Changed;

    public MapSelectionService(OverlaySettingsService settings)
    {
        _settings = settings;
        CurrentMap = MapProviders.Resolve(settings.Load().SelectedMapId);
    }

    public void Select(string id)
    {
        var map = MapProviders.All.FirstOrDefault(p => p.Id == id)
            ?? throw new ArgumentException("Неизвестная карта.", nameof(id));
        if (CurrentMap == map) return;
        // Read-modify-save preserves the latest window sizes, modes and mini-map preferences.
        // Publish the change only after a successful save; a failure leaves the current map intact.
        var settings = _settings.Load();
        settings.SelectedMapId = map.Id;
        _settings.Save(settings);
        CurrentMap = map;
        Changed?.Invoke(map);
    }
}
