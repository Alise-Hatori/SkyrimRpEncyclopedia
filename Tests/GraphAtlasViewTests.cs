using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Views;

static class GraphAtlasViewTests
{
    public static void Run()
    {
        Exception? failure = null;
        var checks = 0;
        var thread = new Thread(() =>
        {
            try
            {
                void Check(bool condition, string message)
                {
                    checks++;
                    if (!condition) throw new InvalidOperationException("Atlas: " + message);
                }
                T Part<T>(GraphView view, string name) where T : FrameworkElement => (T)view.FindName(name);
                void Click(GraphView view, string name) => Part<Button>(view, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                void Layout(GraphView view, double width, double height)
                {
                    view.Measure(new Size(width, height));
                    view.Arrange(new Rect(0, 0, width, height));
                    view.UpdateLayout();
                }

                // Null dependencies make any accidental legacy-service access fail immediately.
                var view = new GraphView(null!, null!, null!);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Check(Part<TextBlock>(view, "StateTitle").Text.Contains("Выберите"), "Initial state lost.");
                Check(!Part<Button>(view, "OpenArticleButton").IsEnabled, "Opening enabled without selection.");
                Check(!typeof(GraphView).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Any(f => new[] { typeof(DatabaseService), typeof(OfficialDatabaseService), typeof(MvpKnowledgeDatabaseService) }.Contains(f.FieldType)),
                    "View retains SQL services.");
                Check(typeof(GraphView).GetMethod("LoadGraph", BindingFlags.NonPublic | BindingFlags.Instance) is null,
                    "Legacy bulk loader remains reachable.");
                var filters = new[] { "ShowOfficialCheckBox", "ShowMvpCheckBox", "ShowPersonalCheckBox", "ShowCharactersCheckBox",
                    "ShowPlacesCheckBox", "ShowFactionsCheckBox", "ShowQuestsCheckBox", "ShowItemsCheckBox", "ShowCreaturesCheckBox",
                    "ShowRacesCheckBox", "ShowSpellsCheckBox", "ShowReligionCheckBox", "ShowOtherCheckBox" };
                foreach (var name in filters)
                {
                    var checkbox = Part<CheckBox>(view, name);
                    Check(checkbox.IsChecked == true, "Filter/default missing: " + name);
                    checkbox.IsChecked = false;
                    checkbox.IsChecked = true;
                }
                Check(Part<TextBlock>(view, "StateDescription").Text.Contains("Запросы к базе не выполняются"), "Filter pretends to load data.");
                Part<TextBox>(view, "SearchBox").Text = "Вайтран";
                Click(view, "SearchButton");
                Check(Part<TextBlock>(view, "StateTitle").Text.Contains("ещё не подключены"), "Fake search result.");

                foreach (var state in Enum.GetValues<AtlasPresentationState>())
                {
                    view.ShowState(state, state == AtlasPresentationState.Error ? "Контрольная ошибка" : null);
                    Check(Part<ProgressBar>(view, "StateProgress").Visibility ==
                        (state == AtlasPresentationState.Loading ? Visibility.Visible : Visibility.Collapsed), "Wrong loading indicator.");
                }
                Check(Part<TextBlock>(view, "StateDescription").Text == "Контрольная ошибка", "Error detail hidden.");
                Article? opened = null;
                var back = 0;
                view.ArticleSelected += article => opened = article;
                view.BackRequested += () => back++;
                var metadata = new Article { Id = 1_000_000_123, IsOfficial = true, IsMvp = true, Title = "Контрольная статья", Category = "Локации" };
                metadata.DeferContent(() => throw new InvalidOperationException("Atlas read article content"));
                view.SetSelectedArticle(metadata);
                Check(opened is null && Part<TextBlock>(view, "SelectedTitle").Text == metadata.Title, "Selection opened article automatically.");
                Check(!metadata.IsContentLoaded, "Metadata selection materialized Content.");
                Click(view, "OpenArticleButton");
                Check(ReferenceEquals(opened, metadata), "Article identity/source lost.");
                Click(view, "BackButton");
                Check(back == 1, "Back event lost.");
                Click(view, "ResetButton");
                Check(Part<TextBox>(view, "SearchBox").Text == "" && !Part<Button>(view, "OpenArticleButton").IsEnabled,
                    "Reset leaves obsolete selection/query.");

                Layout(view, 1280, 800);
                Check(Part<Border>(view, "FiltersPanel").Visibility == Visibility.Visible &&
                    Part<Border>(view, "DetailsPanel").Visibility == Visibility.Visible, "Wide panels missing.");
                Check(Part<Border>(view, "MapSurface").ActualWidth > 450, "Map has no bounded usable width.");
                Preview(view, "atlas-wide.png", 1280, 800);
                Layout(view, 720, 540);
                Check(Part<Border>(view, "FiltersPanel").Visibility == Visibility.Collapsed &&
                    Part<Border>(view, "DetailsPanel").Visibility == Visibility.Collapsed, "Compact panels did not collapse.");
                Check(Part<Border>(view, "MapSurface").ActualWidth > 650, "Compact panels consume map width.");
                Click(view, "FiltersButton");
                Check(Part<Border>(view, "FiltersPanel").Visibility == Visibility.Visible &&
                    Part<Border>(view, "DetailsPanel").Visibility == Visibility.Collapsed, "Filter drawer unavailable.");
                Click(view, "DetailsButton");
                Check(Part<Border>(view, "FiltersPanel").Visibility == Visibility.Collapsed &&
                    Part<Border>(view, "DetailsPanel").Visibility == Visibility.Visible, "Drawers overlap each other.");
                Click(view, "DetailsButton");
                Layout(view, 720, 540);
                Preview(view, "atlas-compact.png", 720, 540);

                var project = FindProject();
                XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                var main = XDocument.Load(Path.Combine(project, "MainWindow.xaml"));
                var host = main.Descendants(wpf + "Grid").Single(e => (string?)e.Attribute(x + "Name") == "GraphContainer");
                Check(!host.Ancestors().Any(e => e.Name == wpf + "ScrollViewer" || e.Name == wpf + "StackPanel"),
                    "Atlas is hosted in unbounded article scroller.");
                Check(main.Descendants(wpf + "Button").Any(e => (string?)e.Attribute("Click") == "Graph_Click"), "Graph entry removed.");
                var source = File.ReadAllText(Path.Combine(project, "MainWindow.xaml.cs"));
                var start = source.IndexOf("private void Graph_Click(", StringComparison.Ordinal);
                var end = source.IndexOf("private void ShowMainContent()", start, StringComparison.Ordinal);
                var entry = source[start..end];
                Check(entry.Contains("GraphContainer.Children.Add(graph)") && entry.Contains("GraphContainer.Visibility = Visibility.Visible") &&
                    entry.Contains("ArticlesScrollViewer.Visibility = Visibility.Collapsed"), "Graph route fails to activate bounded host.");
                Check(entry.Contains("graph.ArticleSelected +=") && entry.Contains("OpenArticle(") && entry.Contains("graph.BackRequested +="),
                    "Article/catalog routes disconnected.");
            }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Atlas shell tests failed.", failure);
        Console.WriteLine($"Atlas shell OK: {checks} checks; no database services/content/bulk loader, states, 13 filters, selection/open separation, bounded responsive layout and navigation contract. No visible window.");
    }

    private static string FindProject()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MainWindow.xaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Cannot find MainWindow source for navigation contract test.");
    }
    private static void Preview(GraphView view, string name, int width, int height)
    {
        var folder = Environment.GetEnvironmentVariable("SKYRIM_RP_GRAPH_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(folder, name);
        using var stream = File.Create(path);
        encoder.Save(stream);
        Console.WriteLine("Atlas offscreen preview: " + path);
    }
}
