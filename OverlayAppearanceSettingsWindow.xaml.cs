using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia;

public partial class OverlayAppearanceSettingsWindow : Window
{
    private bool _isLoading;

    public event Action<string, double, double, bool>? AppearanceChanged;
    public event Action<bool, bool, bool, bool, bool>? BlockVisibilityChanged;
    public event Action<string>? LayoutModeChanged;

    public OverlayAppearanceSettingsWindow(OverlaySettings settings)
    {
        InitializeComponent();
        ThemeComboBox.ItemsSource = OverlayAppearance.Themes;
        LayoutModeComboBox.ItemsSource = OverlayLayoutModes.All;
        LoadSettings(settings);
    }

    private void LoadSettings(OverlaySettings settings)
    {
        _isLoading = true;
        ThemeComboBox.SelectedItem = settings.Theme;
        LayoutModeComboBox.SelectedItem = settings.LayoutMode;
        OpacitySlider.Value = settings.Opacity;
        FontSizeSlider.Value = settings.FontSize;
        DecorativeBorderCheckBox.IsChecked = settings.ShowDecorativeBorder;
        ArticleTitleCheckBox.IsChecked = settings.ShowArticleTitle;
        ArticleCategoryCheckBox.IsChecked = settings.ShowArticleCategory;
        ArticleContentCheckBox.IsChecked = settings.ShowArticleContent;
        RelatedArticlesCheckBox.IsChecked = settings.ShowRelatedArticles;
        EmbeddedMapCheckBox.IsChecked = settings.ShowEmbeddedMap;
        _isLoading = false;
        ApplyTheme(settings.Theme);
        UpdateValueLabels();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => Apply();

    private void LayoutModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading && LayoutModeComboBox.SelectedItem is string layoutMode)
            LayoutModeChanged?.Invoke(layoutMode);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateValueLabels();
        Apply();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateValueLabels();
        Apply();
    }

    private void DecorativeBorderCheckBox_Changed(object sender, RoutedEventArgs e) => Apply();

    private void BlockVisibilityCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        BlockVisibilityChanged?.Invoke(
            ArticleTitleCheckBox.IsChecked == true,
            ArticleCategoryCheckBox.IsChecked == true,
            ArticleContentCheckBox.IsChecked == true,
            RelatedArticlesCheckBox.IsChecked == true,
            EmbeddedMapCheckBox.IsChecked == true);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        LoadSettings(new OverlaySettings());
        Apply();
        BlockVisibilityCheckBox_Changed(this, new RoutedEventArgs());
        if (LayoutModeComboBox.SelectedItem is string layoutMode)
            LayoutModeChanged?.Invoke(layoutMode);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Apply()
    {
        if (_isLoading || ThemeComboBox.SelectedItem is not string theme)
            return;

        ApplyTheme(theme);
        AppearanceChanged?.Invoke(
            theme,
            OpacitySlider.Value,
            FontSizeSlider.Value,
            DecorativeBorderCheckBox.IsChecked == true);
    }

    private void UpdateValueLabels()
    {
        if (OpacityValueText is not null)
            OpacityValueText.Text = $"{OpacitySlider.Value:P0}";
        if (FontSizeValueText is not null)
            FontSizeValueText.Text = $"{FontSizeSlider.Value:0} pt";
    }

    private void ApplyTheme(string theme)
    {
        var palette = OverlayAppearance.GetPalette(theme);
        SetBrush("SettingsBackgroundBrush", palette.Background);
        SetBrush("SettingsBorderBrush", palette.Border);
        SetBrush("SettingsPrimaryTextBrush", palette.PrimaryText);
        SetBrush("SettingsSecondaryTextBrush", palette.SecondaryText);
        SetBrush("SettingsAccentBrush", palette.Accent);
        SetBrush("SettingsHoverBrush", palette.Hover);
    }

    private void SetBrush(string resourceKey, string color) =>
        Resources[resourceKey] = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
}
