using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class OverlayWindow : Window
{
    private readonly OverlaySettingsService _settingsService;
    private bool _isApplyingSettings;
    private bool _isCompactMode;
    private bool _isMapVisiblePreference;
    private bool _isPositionLocked;
    private string _theme = OverlayAppearance.NordicFrost;
    private double _fontSize = 14;
    private bool _showDecorativeBorder = true;
    private bool _showArticleTitle = true;
    private bool _showArticleCategory = true;
    private bool _showArticleContent = true;
    private bool _showRelatedArticles = true;
    private bool _showEmbeddedMap = true;
    private string _layoutMode = OverlayLayoutModes.Normal;

    public event Action<Article>? ArticleSelected;
    public event Action? HideRequested;
    public event Action? MiniMapToggleRequested;
    public Article? DisplayedArticle { get; private set; }

    public OverlayWindow(OverlaySettingsService? settingsService = null)
    {
        OverlayCrashDiagnostics.Write("OverlayWindow.Constructor.Enter", this);
        InitializeComponent();
        _settingsService = settingsService ?? new OverlaySettingsService();

        OverlayMap.ShowBackButton = false;
        ApplySettings(_settingsService.Load());
        DisplayArticle(null);
        LocationChanged += (_, _) => SaveSettings();
        SizeChanged += (_, _) => SaveSettings();
        Closed += (_, _) => SaveSettings();
        Closing += (_, _) => OverlayCrashDiagnostics.Write("OverlayWindow.Closing", this);
        Closed += (_, _) => OverlayCrashDiagnostics.Write("OverlayWindow.Closed", this);
        OverlayCrashDiagnostics.Write("OverlayWindow.Constructor.Exit", this);
    }

    public void DisplayArticle(
        Article? article,
        IEnumerable<OverlayRelatedArticle>? relatedArticles = null)
    {
        DisplayedArticle = article;
        var content = OverlayArticleContent.FromArticle(article);
        ArticleTitleText.Text = content.Title;
        ArticleCategoryText.Text = content.Category;
        ArticleContentText.Text = content.Text;
        SourceButton.Tag = content.SourceUrl;
        SourceButton.Visibility = content.SourceUrl is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        var related = relatedArticles?.ToList() ?? new List<OverlayRelatedArticle>();
        RelatedArticlesItems.ItemsSource = related;
        RelatedEmptyText.Visibility = related.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateEffectiveBlockVisibility();

        // A newly selected article must start at its heading, even if the
        // previous article was read near its end.
        ArticleScrollViewer.ScrollToTop();
    }

    private void ApplySettings(OverlaySettings settings)
    {
        _isApplyingSettings = true;
        Width = settings.Width;
        Height = settings.Height;
        if (settings.Left.HasValue)
            Left = settings.Left.Value;
        if (settings.Top.HasValue)
            Top = settings.Top.Value;

        MapHeightSlider.Value = settings.MapHeight;
        ApplyAppearance(settings.Theme, settings.Opacity, settings.FontSize, settings.ShowDecorativeBorder, false);
        ApplyBlockVisibility(
            settings.ShowArticleTitle,
            settings.ShowArticleCategory,
            settings.ShowArticleContent,
            settings.ShowRelatedArticles,
            settings.ShowEmbeddedMap,
            false);
        ApplyLayoutMode(settings.LayoutMode, false);
        SetMapVisibility(settings.IsMapVisible);
        ApplyCompactMode(settings.IsCompactMode, false);
        _isPositionLocked = settings.IsPositionLocked;
        UpdatePositionLockHint();
        _isApplyingSettings = false;
    }

    private void SaveSettings()
    {
        if (!IsLoaded)
            return;

        var settings = _settingsService.Load();
        settings.Left = Left;
        settings.Top = Top;
        settings.Width = ActualWidth;
        settings.Height = ActualHeight;
        settings.Opacity = Opacity;
        settings.IsMapVisible = _isMapVisiblePreference;
        settings.MapHeight = MapHeightSlider.Value;
        settings.Theme = _theme;
        settings.FontSize = _fontSize;
        settings.ShowDecorativeBorder = _showDecorativeBorder;
        settings.ShowArticleTitle = _showArticleTitle;
        settings.ShowArticleCategory = _showArticleCategory;
        settings.ShowArticleContent = _showArticleContent;
        settings.ShowRelatedArticles = _showRelatedArticles;
        settings.ShowEmbeddedMap = _showEmbeddedMap;
        settings.LayoutMode = _layoutMode;
        settings.IsCompactMode = _isCompactMode;
        settings.IsPositionLocked = _isPositionLocked;
        _settingsService.Save(settings);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isApplyingSettings)
            return;

        Opacity = e.NewValue;
        SaveSettings();
    }

    private void MiniMapToggle_Click(object sender, RoutedEventArgs e) =>
        MiniMapToggleRequested?.Invoke();

    private void MapToggle_Click(object sender, RoutedEventArgs e)
    {
        SetMapVisibility(MapContainer.Visibility != Visibility.Visible);
        SaveSettings();
    }

    private void MapHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MapContainer.Height = e.NewValue;
        SaveSettings();
    }

    private void SetMapVisibility(bool isVisible)
    {
        _isMapVisiblePreference = isVisible;
        MapContainer.Visibility = isVisible && _showEmbeddedMap && _layoutMode == OverlayLayoutModes.Normal && !_isCompactMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        MapControlsPanel.Visibility = _showEmbeddedMap && _layoutMode == OverlayLayoutModes.Normal && !_isCompactMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        MapToggleButton.Content = isVisible ? "Скрыть" : "Показать";
        MapControlToggleButton.Content = isVisible ? "Скрыть карту" : "Показать карту";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPositionLocked && e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OverlayContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        NordCoreMapMenuItem.IsChecked = OverlayMap.SelectedMapId == MapProviders.NordCore;
        SkyRpMapMenuItem.IsChecked = OverlayMap.SelectedMapId == MapProviders.SkyRp;
        CompactModeMenuItem.IsChecked = _isCompactMode;
        PositionLockMenuItem.IsChecked = _isPositionLocked;
    }

    private void SelectMap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { Tag: string id } && !OverlayMap.SelectMap(id))
            MessageBox.Show(this, "Не удалось сохранить выбор карты. Проверьте доступ к файлу настроек. Предыдущая карта сохранена.",
                "Карта Skyrim", MessageBoxButton.OK, MessageBoxImage.Warning);
        NordCoreMapMenuItem.IsChecked = OverlayMap.SelectedMapId == MapProviders.NordCore;
        SkyRpMapMenuItem.IsChecked = OverlayMap.SelectedMapId == MapProviders.SkyRp;
    }

    private void AppearanceSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        settings.Theme = _theme;
        settings.Opacity = Opacity;
        settings.FontSize = _fontSize;
        settings.ShowDecorativeBorder = _showDecorativeBorder;
        settings.ShowArticleTitle = _showArticleTitle;
        settings.ShowArticleCategory = _showArticleCategory;
        settings.ShowArticleContent = _showArticleContent;
        settings.ShowRelatedArticles = _showRelatedArticles;
        settings.ShowEmbeddedMap = _showEmbeddedMap;
        settings.LayoutMode = _layoutMode;

        var dialog = new OverlayAppearanceSettingsWindow(settings)
        {
            Owner = this
        };
        dialog.AppearanceChanged += (theme, opacity, fontSize, showBorder) =>
            ApplyAppearance(theme, opacity, fontSize, showBorder, true);
        dialog.BlockVisibilityChanged += (showTitle, showCategory, showContent, showRelatedArticles, showMap) =>
            ApplyBlockVisibility(showTitle, showCategory, showContent, showRelatedArticles, showMap, true);
        dialog.LayoutModeChanged += layoutMode => ApplyLayoutMode(layoutMode, true);
        dialog.ShowDialog();
    }

    private void CompactModeMenuItem_Click(object sender, RoutedEventArgs e) =>
        ApplyCompactMode(CompactModeMenuItem.IsChecked, true);

    private void PositionLockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _isPositionLocked = PositionLockMenuItem.IsChecked;
        UpdatePositionLockHint();
        SaveSettings();
    }

    private void HideOverlayMenuItem_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke();

    private void ResetAppearanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new OverlaySettings();
        ApplyAppearance(defaults.Theme, defaults.Opacity, defaults.FontSize, defaults.ShowDecorativeBorder, false);
        ApplyBlockVisibility(
            defaults.ShowArticleTitle,
            defaults.ShowArticleCategory,
            defaults.ShowArticleContent,
            defaults.ShowRelatedArticles,
            defaults.ShowEmbeddedMap,
            false);
        ApplyLayoutMode(defaults.LayoutMode, false);
        ApplyCompactMode(false, false);
        SaveSettings();
    }

    private void ApplyCompactMode(bool isCompact, bool save)
    {
        _isCompactMode = isCompact;
        SetMapVisibility(_isMapVisiblePreference);
        if (save)
            SaveSettings();
    }

    private void ApplyAppearance(
        string theme,
        double opacity,
        double fontSize,
        bool showDecorativeBorder,
        bool save)
    {
        var normalized = OverlaySettingsService.Normalize(new OverlaySettings
        {
            Theme = theme,
            Opacity = opacity,
            FontSize = fontSize,
            ShowDecorativeBorder = showDecorativeBorder
        });

        _theme = normalized.Theme;
        _fontSize = normalized.FontSize;
        _showDecorativeBorder = normalized.ShowDecorativeBorder;
        Opacity = normalized.Opacity;
        if (Math.Abs(OpacitySlider.Value - normalized.Opacity) > 0.001)
            OpacitySlider.Value = normalized.Opacity;

        var palette = OverlayAppearance.GetPalette(_theme);
        SetBrush("OverlayBackgroundBrush", palette.Background);
        SetBrush("OverlayBorderBrush", palette.Border);
        SetBrush("OverlayPrimaryTextBrush", palette.PrimaryText);
        SetBrush("OverlaySecondaryTextBrush", palette.SecondaryText);
        SetBrush("OverlayContentTextBrush", palette.ContentText);
        SetBrush("OverlaySurfaceBrush", palette.Surface);
        SetBrush("OverlayButtonBrush", palette.Hover);
        SetBrush("OverlayHoverBrush", palette.Hover);
        SetBrush("OverlayAccentBrush", palette.Accent);
        ApplyContextMenuPalette(palette);

        OverlayBorder.BorderThickness = _showDecorativeBorder ? new Thickness(1) : new Thickness(0);
        ArticleContentText.FontSize = _fontSize;
        ArticleTitleText.FontSize = _fontSize + 4;
        ArticleCategoryText.FontSize = Math.Max(10, _fontSize - 2);
        RelatedHeaderText.FontSize = _fontSize + 1;
        RelatedEmptyText.FontSize = Math.Max(10, _fontSize - 2);
        ApplyLayoutMode(_layoutMode, false);

        if (save && !_isApplyingSettings)
            SaveSettings();
    }

    private void ApplyBlockVisibility(
        bool showTitle,
        bool showCategory,
        bool showContent,
        bool showRelatedArticles,
        bool showEmbeddedMap,
        bool save)
    {
        _showArticleTitle = showTitle;
        _showArticleCategory = showCategory;
        _showArticleContent = showContent;
        _showRelatedArticles = showRelatedArticles;
        _showEmbeddedMap = showEmbeddedMap;

        UpdateEffectiveBlockVisibility();

        if (save && !_isApplyingSettings)
            SaveSettings();
    }

    private void ApplyLayoutMode(string layoutMode, bool save)
    {
        _layoutMode = OverlayLayoutModes.IsSupported(layoutMode)
            ? layoutMode
            : OverlayLayoutModes.Normal;

        var isCompactLayout = _layoutMode == OverlayLayoutModes.Compact;
        var isArticleOnlyLayout = _layoutMode == OverlayLayoutModes.ArticleOnly;
        var isFocusedLayout = isCompactLayout || isArticleOnlyLayout;

        OverlayHeaderPanel.Visibility = isFocusedLayout ? Visibility.Collapsed : Visibility.Visible;
        OverlayControlsPanel.Visibility = isFocusedLayout ? Visibility.Collapsed : Visibility.Visible;
        OverlayHintText.Visibility = isFocusedLayout ? Visibility.Collapsed : Visibility.Visible;
        OverlayBorder.Padding = isCompactLayout ? new Thickness(8) : new Thickness(14);
        ArticleMetadataPanel.Margin = isCompactLayout
            ? new Thickness(0, 0, 0, 6)
            : new Thickness(0, 0, 0, 12);
        ArticleScrollViewer.Margin = isCompactLayout
            ? new Thickness(0, 0, 0, 4)
            : new Thickness(0, 0, 0, 10);
        ArticleContentPanel.Margin = isCompactLayout
            ? new Thickness(4)
            : new Thickness(8);
        ArticleContentText.LineHeight = _fontSize * (isCompactLayout ? 1.35 : isArticleOnlyLayout ? 1.65 : 1.55);

        UpdateEffectiveBlockVisibility();

        if (save && !_isApplyingSettings)
            SaveSettings();
    }

    private void UpdateEffectiveBlockVisibility()
    {
        var isFocusedLayout = _layoutMode != OverlayLayoutModes.Normal;
        ArticleTitleText.Visibility = _showArticleTitle ? Visibility.Visible : Visibility.Collapsed;
        ArticleCategoryText.Visibility = _showArticleCategory ? Visibility.Visible : Visibility.Collapsed;
        ArticleContentText.Visibility = _showArticleContent ? Visibility.Visible : Visibility.Collapsed;
        RelatedArticlesPanel.Visibility = _showRelatedArticles && !isFocusedLayout
            ? Visibility.Visible
            : Visibility.Collapsed;
        SourceButton.Visibility = SourceButton.Tag is string && !isFocusedLayout
            ? Visibility.Visible
            : Visibility.Collapsed;
        SetMapVisibility(_isMapVisiblePreference);
    }

    private void UpdatePositionLockHint() =>
        OverlayHintText.Text = _isPositionLocked
            ? "Положение закреплено. Откройте меню по ПКМ, чтобы разблокировать его."
            : "Перетащите верхнюю панель для перемещения. Размер меняется за правый нижний угол.";

    private void SetBrush(string resourceKey, string color) =>
        Resources[resourceKey] = CreateBrush(color);

    private void ApplyContextMenuPalette(OverlayThemePalette palette)
    {
        OverlayContextMenu.Background = CreateBrush(palette.Background);
        OverlayContextMenu.BorderBrush = CreateBrush(palette.Border);
        OverlayContextMenu.Resources["ContextPrimaryBrush"] = CreateBrush(palette.PrimaryText);
        OverlayContextMenu.Resources["ContextHoverBrush"] = CreateBrush(palette.Hover);
        OverlayContextMenu.Resources["ContextAccentBrush"] = CreateBrush(palette.Accent);
    }

    private static SolidColorBrush CreateBrush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        OverlayCrashDiagnostics.Write("OverlayWindow.HideButton.Click", this);
        HideRequested?.Invoke();
    }

    private void Source_Click(object sender, RoutedEventArgs e)
    {
        if (SourceButton.Tag is not string sourceUrl ||
            !MvpKnowledgeDatabaseService.TryGetSafeSourceUri(sourceUrl, out var sourceUri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = sourceUri!.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть источник:\n\n{ex.Message}",
                "Источник статьи",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RelatedArticle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: OverlayRelatedArticle related })
            ArticleSelected?.Invoke(related.Article);
    }
}
