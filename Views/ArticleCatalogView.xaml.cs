using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Views;

public partial class ArticleCatalogView : UserControl
{
    public event Action<Article>? OpenRequested;
    public event Action<Article>? EditRequested;
    public event Action<Article>? DeleteRequested;
    public ArticleCatalogView() => InitializeComponent();
    private int _presentationVersion;
    public void ShowArticles(IReadOnlyList<Article> articles) { _presentationVersion++; CatalogList.ItemsSource = articles; }
    public double CaptureScrollOffset() => FindScrollViewer(CatalogList)?.VerticalOffset ?? 0;
    public void RestoreScrollOffset(double offset)
    {
        var version = _presentationVersion;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (version == _presentationVersion && double.IsFinite(offset))
                FindScrollViewer(CatalogList)?.ScrollToVerticalOffset(Math.Max(0, offset));
        }));
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(root,i)) is ScrollViewer child) return child;
        return null;
    }
    private void Open_Click(object sender, RoutedEventArgs e) { e.Handled = true; if (((FrameworkElement)sender).DataContext is Article a) OpenRequested?.Invoke(a); }
    private void Edit_Click(object sender, RoutedEventArgs e) { e.Handled = true; if (((FrameworkElement)sender).DataContext is Article a && !a.IsOfficial) EditRequested?.Invoke(a); }
    private void Delete_Click(object sender, RoutedEventArgs e) { e.Handled = true; if (((FrameworkElement)sender).DataContext is Article a && !a.IsOfficial) DeleteRequested?.Invoke(a); }
    private void Card_Click(object sender, MouseButtonEventArgs e) { e.Handled = true; if (((FrameworkElement)sender).DataContext is Article a) OpenRequested?.Invoke(a); }
}
