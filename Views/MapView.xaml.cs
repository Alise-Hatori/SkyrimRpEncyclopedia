using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Views;

public partial class MapView : UserControl
{
    public event Action? BackRequested;
    private bool _showBackButton = true;
    private bool _initializationStarted;
    private bool _subscribed;
    private bool _updatingSelector;
    private string? _requestedMapId;
    private ulong _lastNavigationId;
    private readonly MapSelectionService _mapSelection;

    public string SelectedMapId => _mapSelection.CurrentMap.Id;

    public bool ShowMapControls
    {
        get => MapControls.Visibility == Visibility.Visible;
        set
        {
            MapControls.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            MapStatus.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public bool ShowBackButton
    {
        get => _showBackButton;
        set
        {
            _showBackButton = value;
            if (BackButton is not null)
                BackButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public MapView() : this(MapSelectionService.Shared) { }

    public MapView(MapSelectionService mapSelection)
    {
        _mapSelection = mapSelection;
        InitializeComponent();
        MapSelector.ItemsSource = MapProviders.All;
        UpdateSelector();

        MapBrowser.CoreWebView2InitializationCompleted +=
            MapBrowser_CoreWebView2InitializationCompleted;
        MapBrowser.NavigationStarting += MapBrowser_NavigationStarting;
        MapBrowser.NavigationCompleted += MapBrowser_NavigationCompleted;
        Loaded += MapView_Loaded;
        Unloaded += (_, _) =>
        {
            _mapSelection.Changed -= MapSelection_Changed;
            _subscribed = false;
        };
        IsVisibleChanged += (_, _) => { if (IsVisible) NavigateCurrentMap(); };
    }

    private async void MapView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            _mapSelection.Changed += MapSelection_Changed;
            _subscribed = true;
        }
        UpdateSelector();
        if (_initializationStarted)
        {
            NavigateCurrentMap();
            return;
        }

        _initializationStarted = true;

        try
        {
            var hostWindow = Window.GetWindow(this);
            OverlayCrashDiagnostics.Write(
                "MapView.WebView2.Environment.Before",
                hostWindow,
                $"view={Name}; profile={MapWebView2Environment.UserDataFolderDescription}");

            var environment = await MapWebView2Environment.GetAsync();

            OverlayCrashDiagnostics.Write(
                "MapView.WebView2.Environment.After",
                hostWindow,
                $"view={Name}; profile={MapWebView2Environment.UserDataFolderDescription}");

            OverlayCrashDiagnostics.Write(
                "MapView.WebView2.Ensure.Before",
                hostWindow,
                $"view={Name}; control={MapBrowser.GetType().Name}");

            await MapBrowser.EnsureCoreWebView2Async(environment);

            OverlayCrashDiagnostics.Write(
                "MapView.WebView2.Ensure.After",
                hostWindow,
                $"view={Name}; initialized={MapBrowser.CoreWebView2 is not null}");

            NavigateCurrentMap();
        }
        catch (Exception ex)
        {
            _initializationStarted = false;
            var hostWindow = Window.GetWindow(this);
            OverlayCrashDiagnostics.WriteException(
                "MapView.WebView2.Initialization.Failed",
                ex,
                window: hostWindow);
            MapStatus.Text = "Не удалось запустить WebView2. Проверьте WebView2 Runtime. " +
                "Можно повторить загрузку или открыть карту в браузере. " + ex.Message;
        }
    }

    private void MapBrowser_CoreWebView2InitializationCompleted(
        object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        var hostWindow = Window.GetWindow(this);
        if (e.IsSuccess)
        {
            OverlayCrashDiagnostics.Write(
                "MapView.WebView2.Initialization.Completed",
                hostWindow,
                $"view={Name}; profile={MapWebView2Environment.UserDataFolderDescription}");
            return;
        }

        OverlayCrashDiagnostics.WriteException(
            "MapView.WebView2.Initialization.Failed",
            e.InitializationException ?? new InvalidOperationException(
                "WebView2 initialization failed without an exception."),
            window: hostWindow);
    }

    private void MapBrowser_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        _lastNavigationId = e.NavigationId;
        MapStatus.Text = "Загрузка: " + _mapSelection.CurrentMap.Name + "…";
        var destinationHost = Uri.TryCreate(
            e.Uri,
            UriKind.Absolute,
            out var destination)
            ? destination.Host
            : "invalid";

        OverlayCrashDiagnostics.Write(
            "MapView.WebView2.Navigation.Starting",
            Window.GetWindow(this),
            $"view={Name}; destinationHost={destinationHost}");
    }

    private void MapBrowser_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        OverlayCrashDiagnostics.Write(
            "MapView.WebView2.Navigation.Completed",
            Window.GetWindow(this),
            $"view={Name}; success={e.IsSuccess}; webErrorStatus={e.WebErrorStatus}");
        if (e.NavigationId != _lastNavigationId) return;
        MapStatus.Text = !e.IsSuccess || e.HttpStatusCode >= 400
            ? $"Не удалось загрузить {_mapSelection.CurrentMap.Name}: {e.WebErrorStatus}, HTTP {e.HttpStatusCode}. " +
              "Выберите другую карту, нажмите «Повторить» или откройте её в браузере."
            : _mapSelection.CurrentMap.Id == MapProviders.SkyRp
                ? "SkyRP Map · Вход — на сайте; при проблемах — «В браузере»."
                : "NordCore";
    }

    public bool SelectMap(string id)
    {
        try { _mapSelection.Select(id); UpdateSelector(); return true; }
        catch (Exception ex)
        {
            UpdateSelector();
            MapStatus.Text = "Не удалось сохранить выбор карты. " + ex.Message;
            return false;
        }
    }

    private void UpdateSelector()
    {
        _updatingSelector = true;
        MapSelector.SelectedValue = _mapSelection.CurrentMap.Id;
        _updatingSelector = false;
    }

    private void MapSelection_Changed(MapProvider map)
    {
        UpdateSelector();
        NavigateCurrentMap();
    }

    private void NavigateCurrentMap(bool force = false)
    {
        if (!IsLoaded || !IsVisible || MapBrowser.CoreWebView2 is null) return;
        var map = _mapSelection.CurrentMap;
        if (!force && _requestedMapId == map.Id) return;
        try
        {
            MapStatus.Text = "Загрузка: " + map.Name + "…";
            MapBrowser.CoreWebView2.Navigate(map.Address.AbsoluteUri);
            _requestedMapId = map.Id;
        }
        catch (Exception ex)
        {
            MapStatus.Text = "Не удалось открыть карту. Выберите другую карту или «В браузере». " + ex.Message;
        }
    }

    private void MapSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingSelector && MapSelector.SelectedValue is string id) SelectMap(id);
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (!_initializationStarted) MapView_Loaded(this, new RoutedEventArgs());
        else NavigateCurrentMap(force: true);
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        // Only the registered map root is opened, never an OAuth callback URI/token.
        try { Process.Start(new ProcessStartInfo(_mapSelection.CurrentMap.Address.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { MapStatus.Text = "Не удалось открыть браузер. " + ex.Message; }
    }

    private void Back_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }
}
