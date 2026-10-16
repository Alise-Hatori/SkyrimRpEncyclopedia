using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class MiniMapWindow : Window
{
    private readonly OverlaySettingsService _settingsService;
    private bool _preserveVisibleStateOnClose;

    public event Action? HideRequested;

    public MiniMapWindow(OverlaySettingsService? settingsService = null)
    {
        OverlayCrashDiagnostics.Write("MiniMapWindow.Constructor.Enter", this);
        InitializeComponent();
        _settingsService = settingsService ?? new OverlaySettingsService();

        MiniMap.ShowBackButton = false;
        MiniMap.ShowMapControls = false;
        ApplySettings(_settingsService.Load());
        SourceInitialized += (_, _) => ApplyCircularClip();
        SizeChanged += (_, _) =>
        {
            ApplyCircularClip();
            SaveSettings();
        };
        LocationChanged += (_, _) => SaveSettings();
        IsVisibleChanged += (_, _) => SaveSettings();
        Closed += (_, _) => SaveSettings();
        Closing += (_, _) => OverlayCrashDiagnostics.Write("MiniMapWindow.Closing", this);
        Closed += (_, _) => OverlayCrashDiagnostics.Write("MiniMapWindow.Closed", this);
        OverlayCrashDiagnostics.Write("MiniMapWindow.Constructor.Exit", this);
    }

    private void ApplySettings(OverlaySettings settings)
    {
        ApplyDiameter(settings.MiniMapDiameter);
        Opacity = settings.MiniMapOpacity;
        OpacitySlider.Value = settings.MiniMapOpacity;
        if (settings.MiniMapLeft.HasValue)
            Left = settings.MiniMapLeft.Value;
        if (settings.MiniMapTop.HasValue)
            Top = settings.MiniMapTop.Value;
    }

    private void ApplyDiameter(double diameter)
    {
        Width = diameter;
        Height = diameter;
        ApplyCircularClip();
    }

    private void ApplyCircularClip()
    {
        var diameter = Math.Min(MapSurface.ActualWidth, MapSurface.ActualHeight);
        if (diameter <= 0)
            return;

        MiniMap.Clip = new EllipseGeometry(
            new Point(MapSurface.ActualWidth / 2, MapSurface.ActualHeight / 2),
            diameter / 2,
            diameter / 2);
    }

    private void SaveSettings()
    {
        if (!IsLoaded)
            return;

        var existing = _settingsService.Load();
        existing.IsMiniMapVisible = _preserveVisibleStateOnClose || IsVisible;
        existing.MiniMapLeft = Left;
        existing.MiniMapTop = Top;
        existing.MiniMapDiameter = Math.Min(Width, Height);
        existing.MiniMapOpacity = Opacity;
        _settingsService.Save(existing);
    }

    public void CloseForApplicationShutdown()
    {
        _preserveVisibleStateOnClose = IsVisible;
        OverlayCrashDiagnostics.Write("MiniMapWindow.CloseForApplicationShutdown", this);
        Close();
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void IncreaseSize_Click(object sender, RoutedEventArgs e) =>
        ApplyDiameter(Math.Min(480, Width + 20));

    private void DecreaseSize_Click(object sender, RoutedEventArgs e) =>
        ApplyDiameter(Math.Max(180, Width - 20));

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
        SaveSettings();
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        OverlayCrashDiagnostics.Write("MiniMapWindow.HideButton.Click", this);
        HideRequested?.Invoke();
    }
}
