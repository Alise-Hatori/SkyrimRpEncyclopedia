using System.Xml.Linq;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class MapSelectionTests
{
    public static void Run()
    {
        var count = 0;
        void Check(bool valid, string message)
        {
            count++;
            if (!valid) throw new InvalidOperationException("Map selection: " + message);
        }
        var folder = Path.Combine(Path.GetTempPath(), "skyrim-map-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "settings.json");
            var settings = new OverlaySettingsService(path);
            Check(!File.Exists(path), "Test must start without settings.");
            var selection = new MapSelectionService(settings);
            Check(selection.CurrentMap.Id == MapProviders.NordCore, "First run must default to NordCore.");
            Check(!File.Exists(path), "Reading the default must not create a settings file.");
            Check(MapProviders.All.Count == 2 && MapProviders.All.Select(p => p.Id).Distinct().Count() == 2, "Catalog must have two stable unique keys.");
            Check(MapProviders.Resolve(MapProviders.NordCore).Address.AbsoluteUri == "https://map.nordcore.dev/", "Legacy URL changed.");
            Check(MapProviders.Resolve(MapProviders.SkyRp).Address.AbsoluteUri == "https://skyrpmap.com/", "SkyRP URL incorrect.");
            foreach (var invalid in new string?[] { null, "", "future-map", "https://untrusted.invalid/" })
                Check(MapProviders.Resolve(invalid).Id == MapProviders.NordCore, "Unknown settings must safely fall back.");

            File.WriteAllText(path, "{\"Width\":510,\"Height\":610,\"LayoutMode\":\"Только статья\",\"IsPositionLocked\":true}");
            var legacy = settings.Load();
            Check(legacy.SelectedMapId == MapProviders.NordCore && legacy.Width == 510, "Legacy settings lost compatibility.");
            legacy.IsCompactMode = true;
            legacy.MapHeight = 250;
            legacy.IsMiniMapVisible = true;
            legacy.MiniMapDiameter = 300;
            settings.Save(legacy);
            var notifications = 0;
            selection.Changed += p => { notifications++; Check(p.Id == selection.CurrentMap.Id, "Notification precedes committed state."); };
            selection.Select(MapProviders.SkyRp);
            Check(notifications == 1 && selection.CurrentMap.Name == "SkyRP Map", "Selection was not published.");
            var saved = settings.Load();
            Check(saved.SelectedMapId == MapProviders.SkyRp, "Map was not persisted.");
            Check(saved.Width == 510 && saved.Height == 610 && saved.LayoutMode == OverlayLayoutModes.ArticleOnly && saved.IsCompactMode && saved.IsPositionLocked, "Switching maps changed overlay layout.");
            Check(saved.IsMiniMapVisible && saved.MiniMapDiameter == 300 && saved.MapHeight == 250, "Mini-map preferences changed.");
            Check(new MapSelectionService(settings).CurrentMap.Id == MapProviders.SkyRp, "Restart lost map selection.");
            selection.Select(MapProviders.SkyRp);
            Check(notifications == 1, "Repeated same-map selection must not reload or notify.");
            selection.Select(MapProviders.NordCore);
            Check(notifications == 2 && settings.Load().SelectedMapId == MapProviders.NordCore, "Return to NordCore failed.");
            var before = File.ReadAllBytes(path);
            try { selection.Select("unknown"); throw new InvalidOperationException("Invalid map accepted."); }
            catch (ArgumentException) { Check(before.SequenceEqual(File.ReadAllBytes(path)), "Invalid map modified settings."); }
            saved.SelectedMapId = null!;
            Check(OverlaySettingsService.Normalize(saved).SelectedMapId == MapProviders.NordCore, "Null map ID failed normalization.");
            File.WriteAllText(path, "{");
            Check(new MapSelectionService(settings).CurrentMap.Id == MapProviders.NordCore, "Malformed settings lost default recovery.");

            // A directory is an invalid settings destination: no need to modify real ACLs or databases.
            var failed = new MapSelectionService(new OverlaySettingsService(folder));
            var failedEvents = 0;
            failed.Changed += _ => failedEvents++;
            try { failed.Select(MapProviders.SkyRp); throw new InvalidOperationException("Failed save was accepted."); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Check(failed.CurrentMap.Id == MapProviders.NordCore && failedEvents == 0, "Failed save published an unpersisted map.");
            }

            // Source contracts supplement pure settings tests; these do not launch a WebView or claim UI acceptance.
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Views", "MapView.xaml"))) root = root.Parent;
            if (root is null) throw new DirectoryNotFoundException("Map view source checks require checkout.");
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var view = XDocument.Load(Path.Combine(root.FullName, "Views", "MapView.xaml"));
            Check(view.Descendants(wpf + "ComboBox").Any(p => (string?)p.Attribute("SelectionChanged") == "MapSelector_SelectionChanged"), "Map selector is not wired.");
            foreach (var handler in new[] { "Retry_Click", "OpenExternal_Click", "Back_Click" })
                Check(view.Descendants(wpf + "Button").Any(p => (string?)p.Attribute("Click") == handler), "Missing map action: " + handler);
            var code = File.ReadAllText(Path.Combine(root.FullName, "Views", "MapView.xaml.cs"));
            Check(code.Contains("_mapSelection.Changed += MapSelection_Changed") && code.Contains("_mapSelection.Changed -= MapSelection_Changed") && code.Contains("if (!_subscribed)"), "Map subscription lifetime is not bounded.");
            Check(code.Contains("MapBrowser.CoreWebView2.Navigate(map.Address.AbsoluteUri)") && !code.Contains("CoreWebView2.Navigate(\"https://"), "Map view bypasses registered selection.");
            Check(code.Contains("if (e.NavigationId != _lastNavigationId) return;"), "Stale navigation completion is not guarded.");
            Check(code.Contains("_mapSelection.CurrentMap.Address.AbsoluteUri") && !code.Contains("ProcessStartInfo(e.Uri)"), "External fallback must not launch OAuth callback URLs.");
            var overlay = XDocument.Load(Path.Combine(root.FullName, "OverlayWindow.xaml"));
            Check(overlay.Descendants(wpf + "MenuItem").Count(p => (string?)p.Attribute("Click") == "SelectMap_Click") == 2, "Overlay map selection must remain available in compact modes.");
            Check(File.ReadAllText(Path.Combine(root.FullName, "MiniMapWindow.xaml.cs")).Contains("MiniMap.ShowMapControls = false;"), "Circular mini-map must not display the new toolbar.");
            var overlayCode = File.ReadAllText(Path.Combine(root.FullName, "OverlayWindow.xaml.cs"));
            Check(overlayCode.Contains("!OverlayMap.SelectMap(id)") && overlayCode.Contains("Предыдущая карта сохранена."), "Failed context-menu selection must be reported even while the embedded map is hidden.");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"Map selection OK: {count} offline settings, failure and source-contract checks; no browser/DB/authentication calls.");
    }
}
