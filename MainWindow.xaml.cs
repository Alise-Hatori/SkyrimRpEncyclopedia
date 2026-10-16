using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Views;

namespace SkyrimRpEncyclopedia;

public partial class MainWindow : Window
{
    private readonly DatabaseService _database;
    private readonly OfficialDatabaseService _officialDatabase;
    private readonly MvpKnowledgeDatabaseService _mvpDatabase;
    private readonly KnowledgeAssistantService _knowledgeAssistant;
    private readonly ArticleCatalogReader _catalogReader;
    private readonly CancellationTokenSource _catalogLifetime = new();
    private CancellationTokenSource? _searchCancellation;
    private int _viewVersion;
    private int _reloadVersion;
    private bool _catalogReady;
    private Article? _linksArticle;
    private List<ArticleLink> _cachedArticleLinks = new();

    private MapView? _mapView;
    private RpSimpleEventGeneratorView? _simpleEventGenerator;

    private List<Article> _articles = new();
    private List<Article> _officialArticles = new();
    private List<Article> _mvpArticles = new();
    private List<Article> _currentArticles = new();
    private Article? _openedArticle;
    private string _catalogTitle = "Все статьи";
    private string _catalogDescription = "";
    private string _catalogQuery = "";
    private double _catalogScrollOffset;
    private bool _restoringCatalog;
    private string _catalogDiagnostic = "";
    private OverlayWindow? _overlay;
    private MiniMapWindow? _miniMap;
    private HwndSource? _windowSource;
    private IntPtr _lastExternalWindow;
    private bool _overlayHotKeyRegistered;

    private const int OverlayHotKeyId = 0x534B;
    private const int WmHotKey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkF10 = 0x79;

   public MainWindow()
{
    OverlayCrashDiagnostics.Write("MainWindow.Constructor.Enter", this);
    InitializeComponent();

    UserDatabasePreflight.ValidateDistinct(DatabaseService.ResolveDatabasePath(),OfficialDatabaseService.ResolveDatabasePath(),MvpKnowledgeDatabaseService.ResolveDatabasePath());
    UserDatabasePreflight.ValidateExisting(DatabaseService.ResolveDatabasePath());
    UserDatabasePreflight.ValidateExisting(OfficialDatabaseService.ResolveDatabasePath());
    _database = new DatabaseService();

    _officialDatabase =
        new OfficialDatabaseService();

    _mvpDatabase =
        new MvpKnowledgeDatabaseService();

    _knowledgeAssistant =
        new KnowledgeAssistantService(new OllamaKnowledgeModelClient());

    _catalogReader = new ArticleCatalogReader(_database.DatabasePath, _officialDatabase.DatabasePath, _mvpDatabase.DatabasePath);
    CatalogView.OpenRequested += OpenArticle;
    CatalogView.EditRequested += EditArticle;
    CatalogView.DeleteRequested += DeleteArticle;
    ShowWelcome();
    IsEnabled = false;
    PageDescription.Text = "Загрузка метаданных каталога…";
    Loaded += async (_, _) =>
    {
        await LoadArticlesAsync();
        if (_catalogLifetime.IsCancellationRequested) return;
        _catalogReady = true;
        IsEnabled = true;
        if (PageDescription.Text == "Загрузка метаданных каталога…") PageDescription.Text = "База знаний Skyrim RP";
    };
    OverlayCrashDiagnostics.Write("MainWindow.Created", this);

    SourceInitialized += (_, _) => RegisterOverlayHotKey();
    Loaded += (_, _) => RestoreMiniMapIfRequested();
    Closed += (_, _) =>
    {
        _catalogLifetime.Cancel();
        InvalidatePendingCatalog();
        OverlayCrashDiagnostics.Write("MainWindow.Closed", this);
        UnregisterOverlayHotKey();
    };
    Closing += (_, _) =>
    {
        OverlayCrashDiagnostics.Write("MainWindow.Closing", this);
        OverlayCrashDiagnostics.Write("MainWindow.Closing.CloseOverlay", _overlay);
        _overlay?.Close();
        OverlayCrashDiagnostics.Write("MainWindow.Closing.CloseMiniMap", _miniMap);
        _miniMap?.CloseForApplicationShutdown();
    };
}

    private void Overlay_Click(object sender, RoutedEventArgs e)
    {
        OverlayCrashDiagnostics.Write("MainWindow.OverlayButton.Click", this);
        ToggleOverlay();
    }

    private void ToggleOverlay()
    {
        OverlayCrashDiagnostics.Write("MainWindow.ToggleOverlay.Enter", this);
        if (_overlay?.IsVisible == true)
        {
            OverlayCrashDiagnostics.Write("MainWindow.ToggleOverlay.Hide", _overlay);
            HideOverlay();
            return;
        }

        EnsureOverlay();
        CaptureExternalForegroundWindow();
        OverlayCrashDiagnostics.Write("MainWindow.ToggleOverlay.Show.Before", _overlay);
        _overlay!.Show();
        OverlayCrashDiagnostics.Write("MainWindow.ToggleOverlay.Show.After", _overlay);
        _overlay.Activate();
        OverlayCrashDiagnostics.Write("MainWindow.ToggleOverlay.Activate.After", _overlay);
    }

    private void EnsureOverlay()
    {
        if (_overlay is not null)
            return;

        OverlayCrashDiagnostics.Write("MainWindow.EnsureOverlay.Create.Before", this);
        _overlay = new OverlayWindow();
        OverlayCrashDiagnostics.Write("MainWindow.EnsureOverlay.Create.After", _overlay);
        _overlay.HideRequested += () => HideOverlay();
        _overlay.MiniMapToggleRequested += ToggleMiniMap;
        _overlay.ArticleSelected += OpenOverlayRelatedArticle;
        _overlay.Closed += (_, _) =>
        {
            OverlayCrashDiagnostics.Write("OverlayWindow.Closed.MainSubscription", _overlay);
            _overlay = null;
        };
        UpdateOverlayArticle();
    }

    private void OpenOverlayRelatedArticle(Article article) => OpenArticle(article);

    private void RestoreMiniMapIfRequested()
    {
        if (new OverlaySettingsService().Load().IsMiniMapVisible)
            ShowMiniMap();
    }

    private void ToggleMiniMap()
    {
        if (_miniMap?.IsVisible == true)
            HideMiniMap();
        else
            ShowMiniMap();
    }

    private void ShowMiniMap()
    {
        EnsureMiniMap();
        if (_miniMap!.IsVisible)
            return;

        _miniMap.Show();
        _miniMap.Activate();
    }

    private void HideMiniMap()
    {
        if (_miniMap?.IsVisible == true)
            _miniMap.Hide();
    }

    private void EnsureMiniMap()
    {
        if (_miniMap is not null)
            return;

        _miniMap = new MiniMapWindow();
        _miniMap.HideRequested += HideMiniMap;
        _miniMap.Closed += (_, _) => _miniMap = null;
    }

    private void UpdateOverlayArticle()
    {
        if (_overlay is null)
            return;

        _overlay.DisplayArticle(
            _openedArticle,
            _openedArticle is null
                ? Array.Empty<OverlayRelatedArticle>()
                : OverlayRelatedArticleResolver.Resolve(
                    _openedArticle,
                    GetArticleLinks(_openedArticle),
                    GetAllArticles()));
    }

    private List<ArticleLink> GetArticleLinks(Article article) =>
        _linksArticle == article ? _cachedArticleLinks : ReadArticleLinks(article);

    private List<ArticleLink> ReadArticleLinks(Article article) =>
        article.IsRpRule ? _catalogReader.ReadRuleLinks(article.Id) : article.IsMvp
            ? _mvpDatabase.GetLinksForArticle(article.Id)
            : _database.GetLinksForArticle(article.Id, article.IsOfficial);

    private void HideOverlay(bool restoreExternalFocus = true)
    {
        if (_overlay?.IsVisible != true)
            return;

        var targetWindow = _lastExternalWindow;
        OverlayCrashDiagnostics.Write("MainWindow.HideOverlay.Before", _overlay, $"restoreExternalFocus={restoreExternalFocus}; targetWindow={targetWindow}");
        _overlay.Hide();
        OverlayCrashDiagnostics.Write("MainWindow.HideOverlay.After", _overlay);

        if (!restoreExternalFocus || !IsWindow(targetWindow))
            return;

        Dispatcher.BeginInvoke(
            () =>
            {
                if (_overlay?.IsVisible != true && IsWindow(targetWindow))
                    _ = SetForegroundWindow(targetWindow);
            },
            DispatcherPriority.ApplicationIdle);
    }

    private void CaptureExternalForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        var mainHandle = new WindowInteropHelper(this).Handle;
        var overlayHandle = _overlay is null
            ? IntPtr.Zero
            : new WindowInteropHelper(_overlay).Handle;
        if (foreground != IntPtr.Zero && foreground != mainHandle && foreground != overlayHandle && IsWindow(foreground))
            _lastExternalWindow = foreground;
    }

    private void RegisterOverlayHotKey()
    {
        var handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource?.AddHook(WindowMessageHook);
        _overlayHotKeyRegistered = RegisterHotKey(handle, OverlayHotKeyId, ModControl | ModShift, VkF10);
    }

    private void UnregisterOverlayHotKey()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (_overlayHotKeyRegistered && handle != IntPtr.Zero)
            _ = UnregisterHotKey(handle, OverlayHotKeyId);

        _overlayHotKeyRegistered = false;

        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == OverlayHotKeyId)
        {
            OverlayCrashDiagnostics.Write("MainWindow.OverlayHotKey", this);
            ToggleOverlay();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

private async Task TestWebImportAsync()
{
    try
    {
        var importer =
            new SkyrimImportService(_database);

        var result =
    await importer.ImportPage(
        "https://en.uesp.net/wiki/Skyrim:Alftand",
        "важные места");

        if (result == null)
        {
            MessageBox.Show(
                "Импорт не вернул статью.",
                "Импорт",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        MessageBox.Show(
            $"Статья импортирована.\n\n" +
            $"ID: {result.ArticleId}\n" +
            $"Название: {result.Title}\n" +
            $"Режим: {(result.Updated ? "обновлена" : "добавлена")}",
            "Импорт UESP",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            "Ошибка импорта:\n\n" +
            ex,
            "Импорт UESP",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

    private async Task<bool> LoadArticlesAsync()
    {
        var version = ++_reloadVersion;
        var view = _viewVersion;
        try
        {
            // Reader owns independent read-only connections; only plain Article metadata crosses threads.
            var snapshot = await Task.Run(() =>
            {
                var items=new List<Article>(); var warnings=new List<string>();
                foreach(var source in new[]{ArticleCatalogSource.Official,ArticleCatalogSource.Knowledge,ArticleCatalogSource.User,ArticleCatalogSource.Rules})
                {
                    try {items.AddRange(_catalogReader.ReadSourceMetadata(source,cancellationToken:_catalogLifetime.Token));}
                    catch(Exception error) when((source==ArticleCatalogSource.Knowledge || source==ArticleCatalogSource.Rules) && error is not OperationCanceledException)
                    { warnings.Add("Knowledge DB недоступна: "+error.Message); OverlayCrashDiagnostics.WriteException("Catalog.Knowledge.Read.Failed",error); }
                }
                if(!_mvpDatabase.IsAvailable) warnings.Add("Knowledge DB не подключена: укажите существующий файл в SKYRIM_RP_MVP_DB. Пустая база не создаётся.");
                return (Items:RpRulesCatalog.PreferPersonalCopies(items),Warning:string.Join(" ",warnings));
            },_catalogLifetime.Token);
            if (_catalogLifetime.IsCancellationRequested || version != _reloadVersion) return false;
            var rows=snapshot.Items; _catalogDiagnostic=snapshot.Warning;
            _articles = rows.Where(a => !a.IsOfficial).ToList();
            _officialArticles = rows.Where(a => a.IsOfficial && !a.IsMvp).ToList();
            _mvpArticles = rows.Where(a => a.IsMvp).ToList();
            if(_catalogDiagnostic.Length>0) PageDescription.Text=_catalogDiagnostic;
            return view == _viewVersion;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception)
        {
            if (!_catalogLifetime.IsCancellationRequested && view == _viewVersion)
                PageDescription.Text = "Не удалось загрузить каталог: " + exception.Message;
            return false;
        }
    }

    private void InvalidatePendingCatalog()
    {
        _viewVersion++;
        _searchCancellation?.Cancel();
    }

    private void ClearArticlesPanel()
    {
        InvalidatePendingCatalog();
        if (GraphContainer.Visibility == Visibility.Visible)
            ArticlesScrollViewer.Visibility = Visibility.Visible;
        GraphContainer.Visibility = Visibility.Collapsed;
        GraphContainer.Children.Clear();
        CatalogView.Visibility = Visibility.Collapsed;
        CatalogView.ShowArticles(Array.Empty<Article>());
        ArticlesPanel.Children.Clear();
    }

    private List<Article> GetAllArticles()
    {
        return
            _officialArticles
                .Concat(_mvpArticles)
                .Concat(_articles)
                .ToList();
    }

    private static string GetArticleSourceLabel(Article article) =>
        article.IsRpRule ? "RP-правила сообщества · не канон Skyrim Wiki" : article.IsMvp
            ? "MVP-база"
            : article.IsOfficial
            ? "Официальная статья"
            : "Личная статья";

    private static string GetArticleSourceBadge(Article article) =>
        article.IsRpRule ? "RP-ПРАВИЛА · НЕ КАНОН WIKI" : article.IsMvp
            ? "MVP-БАЗА"
            : article.IsOfficial
            ? "ОФИЦИАЛЬНЫЙ АРХИВ"
            : "ЛИЧНЫЙ МАТЕРИАЛ";

    private static string GetArticleSourceIcon(Article article) =>
        article.IsMvp ? "🧭" : article.IsOfficial ? "📚" : "👤";

    private void AllArticles_Click(
        object sender,
        RoutedEventArgs e)
    {
        var allArticles =
            GetAllArticles();

        PageTitle.Text =
            "Все статьи";

        PageDescription.Text =
            $"В базе найдено статей: {allArticles.Count}";

        ShowArticles(
            allArticles);
    }

    private void Home_Click(
        object sender,
        RoutedEventArgs e) => ShowWelcome();

    private void Assistant_Click(object sender, RoutedEventArgs e) => ShowKnowledgeAssistant();

    private void ShowKnowledgeAssistant()
    {
        WelcomeContainer.Visibility = Visibility.Collapsed;
        MapContainer.Visibility = Visibility.Collapsed;
        ArticlesScrollViewer.Visibility = Visibility.Visible;

        PageTitle.Text = "ИИ-помощник Skyrim";
        SetActiveSection(PageTitle.Text);
        PageDescription.Text = "Ответы формируются только из локальной базы знаний и сопровождаются проверяемыми источниками.";
        ClearArticlesPanel();

        var questionBox = new TextBox
        {
            MinHeight = 92,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Style = (Style)FindResource("NordSearchBox"),
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(14),
        };

        var askButton = new Button
        {
            Content = "Спросить локальную модель",
            Style = (Style)FindResource("NordPrimaryButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 12),
        };

        var status = new TextBlock
        {
            Style = (Style)FindResource("NordMetadataText"),
            Text = $"Поиск выполняется только по {_mvpArticles.Count} статьям подключённой базы знаний.",
            TextWrapping = TextWrapping.Wrap,
        };

        var answer = new TextBlock
        {
            Style = (Style)FindResource("NordReaderContent"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 12),
        };

        var sourcesPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        askButton.Click += async (_, e) =>
        {
            e.Handled = true;
            askButton.IsEnabled = false;
            status.Text = "Ищу статьи и запрашиваю локальную Ollama…";
            answer.Text = string.Empty;
            sourcesPanel.Children.Clear();

            try
            {
                var result = await _knowledgeAssistant.AskAsync(questionBox.Text, _mvpArticles);
                answer.Text = result.Message;
                status.Text = result.IsInsufficientContext
                    ? "Подходящего подтверждённого контекста не найдено."
                    : result.IsModelUnavailable
                    ? "Контекст найден, но локальная модель не ответила."
                    : $"Ответ сформирован только по {result.Sources.Count} найденным источникам.";
                AddAssistantSources(sourcesPanel, result.Sources);
            }
            catch (Exception ex)
            {
                status.Text = "Не удалось обработать запрос.";
                answer.Text = $"Непредвиденная ошибка локального помощника: {ex.Message}";
            }
            finally
            {
                askButton.IsEnabled = true;
            }
        };

        ArticlesPanel.Children.Add(new TextBlock
        {
            Text = "Вопрос",
            Style = (Style)FindResource("NordReaderTitle"),
            FontSize = 20,
            Margin = new Thickness(0, 0, 0, 8),
        });
        ArticlesPanel.Children.Add(questionBox);
        ArticlesPanel.Children.Add(askButton);
        ArticlesPanel.Children.Add(status);
        ArticlesPanel.Children.Add(answer);
        ArticlesPanel.Children.Add(sourcesPanel);
    }

    private void AddAssistantSources(
        Panel panel,
        IReadOnlyList<KnowledgeAssistantSource> sources)
    {
        if (sources.Count == 0)
            return;

        panel.Children.Add(new TextBlock
        {
            Text = "Использованные источники",
            Style = (Style)FindResource("NordReaderTitle"),
            FontSize = 20,
            Margin = new Thickness(0, 8, 0, 8),
        });

        foreach (var source in sources)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var articleButton = new Button
            {
                Content = source.Title,
                Style = (Style)FindResource("NordReaderBackButton"),
                ToolTip = source.Category,
            };
            articleButton.Click += (_, e) =>
            {
                e.Handled = true;
                OpenArticle(source.Article);
            };
            row.Children.Add(articleButton);

            if (MvpKnowledgeDatabaseService.TryGetSafeSourceUri(source.SourceUrl, out var sourceUri))
            {
                var sourceButton = new Button
                {
                    Content = "↗ Оригинал",
                    Style = (Style)FindResource("NordReaderBackButton"),
                    Margin = new Thickness(8, 0, 0, 0),
                };
                sourceButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = sourceUri!.AbsoluteUri, UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось открыть источник:\n\n{ex.Message}", "Источник", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                row.Children.Add(sourceButton);
            }
            panel.Children.Add(row);
        }
    }

    private void Category_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        string buttonText =
            button.Tag?.ToString()
            ?? button.Content?.ToString()
            ?? "";

        string category =
            buttonText switch
            {
                var x when x.Contains("Фракции")
                    => "Фракции",

                var x when x.Contains("Локации")
                    => "Локации",

                var x when x.Contains("Персонажи")
                    => "Персонажи",

                var x when x.Contains("Правила RP")
                    => "Правила RP",

                var x when x.Contains("Лор Skyrim")
                    => "Лор Skyrim",

                var x when x.Contains("Боги и даэдра")
                    => "Боги и даэдра",

                _ => buttonText
            };

        PageTitle.Text =
            category;

        PageDescription.Text =
            "Материалы выбранной категории.";

        var filtered =
            GetAllArticles()
                .Where(a => ArticleSectionMatcher.Matches(category, a))
                .ToList();

        ShowArticles(
            filtered);
    }

    private void ShowArticles(List<Article> articles, bool restoreContext = false)
    {
        InvalidatePendingCatalog();
        WelcomeContainer.Visibility = Visibility.Collapsed;
        MapContainer.Visibility = Visibility.Collapsed;
        ClearArticlesPanel();
        _currentArticles = articles;
        CatalogView.ShowArticles(articles);
        CatalogView.Visibility = articles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ArticlesScrollViewer.Visibility = articles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (articles.Count == 0)
            ArticlesPanel.Children.Add(CreateEmptyState("Архив пока пуст", "Измените запрос или фильтр. Все статьи остаются доступны в каталоге."));
        PageDescription.Text = $"Найдено статей: {articles.Count}";
        if(_catalogDiagnostic.Length>0) PageDescription.Text+=" · "+_catalogDiagnostic;
        if (restoreContext)
        {
            PageTitle.Text = _catalogTitle;
            PageDescription.Text = _catalogDescription;
            _restoringCatalog = true;
            try { SearchBox.Text = _catalogQuery; }
            finally { _restoringCatalog = false; }
            CatalogView.RestoreScrollOffset(_catalogScrollOffset);
        }
        else
        {
            _catalogTitle = PageTitle.Text;
            _catalogDescription = PageDescription.Text;
            _catalogQuery = SearchBox.Text;
            _catalogScrollOffset = 0;
        }
        SetActiveSection(PageTitle.Text);
    }
    private Border CreateEmptyState(string title, string description)
    {
        var container = new Border
        {
            Style = (Style)FindResource("NordEmptyState"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var content = new StackPanel
        {
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        content.Children.Add(new TextBlock
        {
            Text = "ᛟ",
            FontFamily = (FontFamily)FindResource("NordDisplayFont"),
            FontSize = 38,
            Foreground = (Brush)FindResource("NordBlueGlowBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });
        content.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)FindResource("NordArticleTitle"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = description,
            Style = (Style)FindResource("NordMetadataText"),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 16)
        });

        var createButton = new Button
        {
            Content = "＋ Создать статью",
            Style = (Style)FindResource("NordOpenButton"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        createButton.Click += CreateArticle_Click;
        content.Children.Add(createButton);
        container.Child = content;
        return container;
    }

    private void OpenArticle(Article article) => OpenArticle(article, null);
    private async void OpenArticle(Article article, List<ArticleLink>? suppliedLinks)
    {
        if (CatalogView.Visibility == Visibility.Visible)
            _catalogScrollOffset = CatalogView.CaptureScrollOffset();
        InvalidatePendingCatalog();
        var version = _viewVersion;
        PageDescription.Text = "Загрузка статьи…";
        try
        {
            var links = await Task.Run(() => { _ = article.Content; return suppliedLinks ?? ReadArticleLinks(article); }, _catalogLifetime.Token);
            if (_catalogLifetime.IsCancellationRequested || version != _viewVersion) return;
            _linksArticle = article;
            _cachedArticleLinks = links;
            RenderArticle(article);
            var historyVersion = _viewVersion;
            var openedAt = DateTime.Now;
            try { await Task.Run(() => _database.RecordArticleView(article.Id,article.IsOfficial,openedAt)); }
            catch (Exception historyError)
            {
                OverlayCrashDiagnostics.WriteException("History.Record.Failed",historyError);
                if (historyVersion == _viewVersion) PageDescription.Text += " · Не удалось сохранить историю просмотра; статья открыта.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!_catalogLifetime.IsCancellationRequested && version == _viewVersion)
                PageDescription.Text = "Не удалось открыть статью: " + exception.Message;
        }
    }

    private void RenderArticle(
        Article article)
    {
        CatalogView.Visibility = Visibility.Collapsed;
        WelcomeContainer.Visibility = Visibility.Collapsed;
        MapContainer.Visibility = Visibility.Collapsed;
        ArticlesScrollViewer.Visibility = Visibility.Visible;
        _openedArticle = article;
        UpdateOverlayArticle();

        bool isOfficial =
            article.IsOfficial;

        PageTitle.Text =
            article.Title;

        PageDescription.Text =
            $"{article.Category} • " +
            GetArticleSourceLabel(article);

        ClearArticlesPanel();

        var articlePanel =
            new StackPanel
            {
                Margin =
                    new Thickness(15)
            };

        var navigationPanel =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        20)
            };

        var backButton =
            new Button
            {
                Content =
                    "← Назад",

                Style = (Style)FindResource("NordReaderBackButton"),

                Margin =
                    new Thickness(
                        0,
                        0,
                        10,
                        0),

                Cursor =
                    Cursors.Hand
            };

        backButton.Click += (_, e) =>
        {
            e.Handled = true;

            ShowArticles(
                _currentArticles, restoreContext: true);
        };

        navigationPanel.Children.Add(
            backButton);

        if (!article.IsMvp && !article.IsRpRule)
        {
        var favoriteButton =
            new Button
            {
                Content =
                    article.IsFavorite
                        ? "★ Убрать из избранного"
                        : "☆ В избранное",

                Style = (Style)FindResource("NordReaderFavoriteButton"),

                Cursor =
                    Cursors.Hand
            };

        favoriteButton.Click += (_, e) =>
        {
            e.Handled = true;

            ToggleFavorite(
                article);
        };

        navigationPanel.Children.Add(
            favoriteButton);
        }

        articlePanel.Children.Add(
            navigationPanel);

        articlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    article.Title,

                Style = (Style)FindResource("NordReaderTitle")
            });

        articlePanel.Children.Add(new Border
        {
            Style = (Style)FindResource(
                article.IsMvp
                    ? "NordMvpSourceBadge"
                    : isOfficial
                    ? "NordOfficialSourceBadge"
                    : "NordPersonalSourceBadge"),
            Child = new TextBlock
            {
                Text = GetArticleSourceBadge(article),
                Foreground = article.IsMvp
                    ? Brushes.LightSkyBlue
                    : isOfficial
                    ? Brushes.Goldenrod
                    : Brushes.Plum,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        });

        articlePanel.Children.Add(
            new TextBlock
            {
                Text = $"Раздел архива • {article.Category}",

                Style = (Style)FindResource("NordMetadataText"),

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        20)
            });

        if (article.IsMvp &&
            MvpKnowledgeDatabaseService.TryGetSafeSourceUri(article.SourceUrl, out var sourceUri))
        {
            var sourceButton = new Button
            {
                Content = "↗ Открыть оригинал",
                Width = 180,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 18),
                Style = (Style)FindResource("NordReaderBackButton"),
                Cursor = Cursors.Hand
            };
            sourceButton.Click += (_, e) =>
            {
                e.Handled = true;
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
                        "Источник MVP",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
            articlePanel.Children.Add(sourceButton);
        }

        if (!string.IsNullOrWhiteSpace(
                article.Flags))
        {
            articlePanel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"🚩 {article.Flags}",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                190,
                                150,
                                100)),

                    FontSize = 14,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            15)
                });
        }

        if (!string.IsNullOrWhiteSpace(
                article.Summary))
        {
            articlePanel.Children.Add(
                new TextBlock
                {
                    Text =
                        article.Summary,

                Style = (Style)FindResource("NordReaderSummary")
                });
        }

        articlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    article.Content,

                Style = (Style)FindResource("NordReaderContent")
            });

        if (!string.IsNullOrWhiteSpace(
                article.Tags))
        {
            articlePanel.Children.Add(
                new TextBlock
                {
                    Text =
                        article.Tags,

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                120,
                                150,
                                160)),

                    FontSize = 14,

                    Margin =
                        new Thickness(
                            0,
                            30,
                            0,
                            0)
                });
        }

        AddRelatedArticles(
            article,
            articlePanel);

        ArticlesPanel.Children.Add(
            articlePanel);
    }

    private void AddRelatedArticles(
        Article article,
        StackPanel articlePanel)
    {
        articlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    "Связанные материалы",

                Style = (Style)FindResource("NordArticleTitle"),

                Margin =
                    new Thickness(
                        0,
                        35,
                        0,
                        15)
            });

        var links = GetArticleLinks(article);
        var relatedArticles = GetAllArticles().ToLookup(a => (a.Id, a.IsOfficial));

        if (links.Count == 0)
        {
            articlePanel.Children.Add(
                new TextBlock
                {
                    Text =
                        "Связанных материалов пока нет.",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                130,
                                130,
                                140)),

                    FontSize = 14,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            15)
                });
        }
        else
        {
            foreach (var link in links)
{
    bool sourceMatches =
        link.SourceArticleId ==
            article.Id &&
        link.SourceIsOfficial ==
            article.IsOfficial;

    int relatedId =
        sourceMatches
            ? link.TargetArticleId
            : link.SourceArticleId;

    bool relatedOfficial =
        sourceMatches
            ? link.TargetIsOfficial
            : link.SourceIsOfficial;

    var related = relatedArticles[(relatedId, relatedOfficial)].FirstOrDefault();

    if (related == null)
        continue;

    string relation =
        link.LinkType;

    var row =
        new Grid
        {
            Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    8)
        };

    row.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

    row.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = GridLength.Auto
        });

    row.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = GridLength.Auto
        });

    var openButton =
        new Button
        {
            Content =
                $"{GetArticleSourceIcon(related)} " +
                $"{related.Title}   •   {relation}",

            Style = (Style)FindResource("NordRelatedArticleButton"),

            Cursor =
                Cursors.Hand
        };

    openButton.Click += (_, e) =>
    {
        e.Handled = true;

        OpenArticle(
            related);
    };

    Grid.SetColumn(
        openButton,
        0);

    row.Children.Add(
        openButton);

    var editButton =
        new Button
        {
            Content = "✎",

            Width = 38,
            Height = 42,

            Margin =
                new Thickness(
                    4,
                    0,
                    0,
                    0),

            Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        45,
                        45,
                        52)),

            Foreground =
                Brushes.White,

            BorderThickness =
                new Thickness(0),

            Cursor =
                Cursors.Hand,

            ToolTip =
                "Редактировать связь"
        };

    editButton.Click += async (_, e) =>
    {
        e.Handled = true;

        var editor =
            new ArticleLinkEditorWindow(
                _database,
                article,
                GetAllArticles(),
                link);

        editor.Owner =
            this;

        if (editor.ShowDialog() == true)
        {
            if (!await LoadArticlesAsync()) return;

            var updated =
                GetAllArticles()
                    .FirstOrDefault(
                        a =>
                            a.Id ==
                                article.Id &&
                            a.IsOfficial ==
                                article.IsOfficial);

            if (updated != null)
                OpenArticle(updated);
        }
    };

    Grid.SetColumn(
        editButton,
        1);

    row.Children.Add(
        editButton);

    var deleteButton =
        new Button
        {
            Content = "🗑",

            Width = 38,
            Height = 42,

            Margin =
                new Thickness(
                    4,
                    0,
                    0,
                    0),

            Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        55,
                        35,
                        35)),

            Foreground =
                Brushes.White,

            BorderThickness =
                new Thickness(0),

            Cursor =
                Cursors.Hand,

            ToolTip =
                "Удалить связь"
        };

    deleteButton.Click += async (_, e) =>
    {
        e.Handled = true;

        var result =
            MessageBox.Show(
                $"Удалить связь с «{related.Title}»?",
                "Удаление связи",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _database.DeleteLink(
            link.Id);

        if (!await LoadArticlesAsync()) return;

        var updated =
            GetAllArticles()
                .FirstOrDefault(
                    a =>
                        a.Id ==
                            article.Id &&
                        a.IsOfficial ==
                            article.IsOfficial);

        if (updated != null)
            OpenArticle(updated);
    };

    Grid.SetColumn(
        deleteButton,
        2);

    row.Children.Add(
        deleteButton);

    articlePanel.Children.Add(
        row);
}
        }

        if (!article.IsOfficial)
        {
            var addLinkButton =
                new Button
                {
                    Content =
                        "＋ Добавить связь",

                    Width = 170,

                    Height = 36,

                    HorizontalAlignment =
                        HorizontalAlignment.Left,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                55,
                                48,
                                35)),

                    Foreground =
                        Brushes.White,

                    BorderThickness =
                        new Thickness(0),

                    Cursor =
                        Cursors.Hand,

                    Margin =
                        new Thickness(
                            0,
                            10,
                            0,
                            0)
                };

            addLinkButton.Click += async (_, e) =>
            {
                e.Handled = true;

                var editor =
                    new ArticleLinkEditorWindow(
                        _database,
                        article,
                        GetAllArticles());

                editor.Owner =
                    this;

                if (editor.ShowDialog() == true)
                {
                    if (!await LoadArticlesAsync()) return;

                    var updated =
                        GetAllArticles()
                            .FirstOrDefault(
                                a =>
                                    a.Id ==
                                        article.Id &&
                                    a.IsOfficial ==
                                        article.IsOfficial);

                    if (updated != null)
                        OpenArticle(updated);
                }
            };

            articlePanel.Children.Add(
                addLinkButton);
        }
    }

    private async void ToggleFavorite(
        Article article)
    {
        if (article.IsMvp || article.IsRpRule)
            return;
        article.IsFavorite =
            !article.IsFavorite;
        try
        {
        if (article.IsOfficial)
        {
            _officialDatabase.SetFavorite(
                article.Id,
                article.IsFavorite);
        }
        else
        {
            _database.SetFavorite(
                article.Id,
                article.IsFavorite);
        }
        }
        catch(Exception error)
        { article.IsFavorite=!article.IsFavorite; UserDataErrors.Report("Favorite.Save.Failed",error,this,false);return; }

        if (!await LoadArticlesAsync()) return;

        var updated =
            GetAllArticles()
                .FirstOrDefault(
                    a =>
                        a.Id ==
                            article.Id &&
                        a.IsOfficial ==
                            article.IsOfficial);

        if (updated != null)
            OpenArticle(updated);
    }

    private async void EditArticle(
        Article article)
    {
        try { await Task.Run(() => { _ = article.Content; }, _catalogLifetime.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception exception) { PageDescription.Text = "Не удалось прочитать статью для изменения: " + exception.Message; return; }
        var editor =
            new ArticleEditorWindow(
                _database,
                article);

        editor.Owner =
            this;

        if (editor.ShowDialog() == true)
        {
            if (!await LoadArticlesAsync()) return;

            MyArticles_Click(
                this,
                new RoutedEventArgs());
        }
    }

    private async void DeleteArticle(
        Article article)
    {
        var result =
            MessageBox.Show(
                $"Удалить статью «{article.Title}»?",
                "Удаление статьи",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try { _database.DeleteArticle(article.Id); }
        catch(Exception error) { UserDataErrors.Report("Article.Delete.Failed",error,this,false);return; }

        if (!await LoadArticlesAsync()) return;

        MyArticles_Click(
            this,
            new RoutedEventArgs());
    }

    private void EventsLibrary_Click(object sender, RoutedEventArgs e)
    {
        ShowMainContent();
        PageTitle.Text="Мои ивенты"; SetActiveSection(PageTitle.Text); PageDescription.Text="Просмотр, редактирование и удаление сохранённых RP-событий.";
        try
        {
            var library=new EventsLibraryView(_database);
            library.BackRequested+=()=>{ShowMainContent();AllArticles_Click(this,new RoutedEventArgs());};
            library.EditRequested+=saved=>
            {
                ShowMainContent(); PageTitle.Text="Редактирование ивента"; SetActiveSection("Мои ивенты");
                var editor=new EventBuilderView(_database,saved);
                editor.BackRequested+=()=>EventsLibrary_Click(this,new RoutedEventArgs());
                ArticlesPanel.Children.Add(editor);
            };
            ArticlesPanel.Children.Add(library);
        }
        catch(Exception error)
        {
            OverlayCrashDiagnostics.WriteException("Events.Read.Failed",error);
            PageDescription.Text="Не удалось прочитать библиотеку ивентов: "+error.Message;
        }
    }

    private void Events_Click(
    object sender,
    RoutedEventArgs e)
{
    ShowMainContent();
    var eventBuilder =
        new EventBuilderView(
            _database);

    eventBuilder.BackRequested +=
        () =>
        {
            ShowMainContent();

            AllArticles_Click(
                this,
                new RoutedEventArgs());
        };

    PageTitle.Text =
        "Конструктор ивентов";
    SetActiveSection(PageTitle.Text);

    PageDescription.Text =
        "Создание готовых RP-событий.";

    ClearArticlesPanel();

    ArticlesPanel.Children.Add(
        eventBuilder);
}

    private void Generator_Click(
    object sender,
    RoutedEventArgs e)
{
    ShowMainContent();
    var scenarioCatalog = new ScenarioKnowledgeCatalog(_mvpDatabase.DatabasePath);
    var generator = new ScenarioGeneratorView(
        new ScenarioGeneratorService(scenarioCatalog),
        scenarioCatalog);

    PageTitle.Text =
        "Генератор";
    SetActiveSection(PageTitle.Text);

    PageDescription.Text =
        "Генерация RP-ситуаций.";

    MapContainer.Visibility =
        Visibility.Collapsed;

    ArticlesScrollViewer.Visibility =
        Visibility.Visible;

    ClearArticlesPanel();

    ArticlesPanel.Children.Add(generator);
}

    private void SimpleEventGenerator_Click(object sender, RoutedEventArgs e)
    {
        ClearArticlesPanel(); // Cancels pending article search before changing sections.
        WelcomeContainer.Visibility = Visibility.Collapsed;
        MapContainer.Visibility = Visibility.Collapsed;
        ArticlesScrollViewer.Visibility = Visibility.Visible;
        PageTitle.Text = "Генератор простых ивентов";
        SetActiveSection(PageTitle.Text);
        PageDescription.Text = "Небольшие RP-встречи без обязательного конфликта. Ничего не сохраняется в базы.";
        _simpleEventGenerator ??= new RpSimpleEventGeneratorView();
        ArticlesPanel.Children.Add(_simpleEventGenerator);
    }

    private void Graph_Click(
        object sender,
        RoutedEventArgs e)
    {
        var graph =
            new GraphView(
                _database,
                _officialDatabase,
                _mvpDatabase);

        graph.ArticleSelected +=
            article =>
            {
                ShowMainContent();

                // The bounded reader supplies the exact source-backed lazy Article.
                OpenArticle(article, graph.GetLoadedLinks(article));
            };

        graph.BackRequested +=
            () =>
            {
                ShowMainContent();

                AllArticles_Click(
                    this,
                    new RoutedEventArgs());
            };

        PageTitle.Text =
            "Атлас знаний";
        SetActiveSection(PageTitle.Text);

        PageDescription.Text =
            "Исследование ближайшего окружения сущностей Skyrim.";

        ClearArticlesPanel();

        WelcomeContainer.Visibility = Visibility.Collapsed;
        MapContainer.Visibility = Visibility.Collapsed;
        ArticlesScrollViewer.Visibility = Visibility.Collapsed;
        GraphContainer.Children.Add(graph);
        GraphContainer.Visibility = Visibility.Visible;
    }

    private void ShowMainContent()
{
    WelcomeContainer.Visibility = Visibility.Collapsed;
    WelcomeContainer.Visibility =
        Visibility.Collapsed;

    MapContainer.Visibility =
        Visibility.Collapsed;

    ArticlesScrollViewer.Visibility =
        Visibility.Visible;

    ClearArticlesPanel();
}

    private void SetActiveSection(string title)
    {
        var label = title switch
        {
            "Энциклопедия" => "Главная",
            "Карта Skyrim" => "Карта",
            "Атлас знаний" => "Граф связей",
            "Поиск" or "Фильтр" => "Все статьи",
            _ => title
        };
        foreach (var button in SidebarButtons(SidebarNavigation))
        {
            var text = button.Content is Panel panel ? panel.Children.OfType<TextBlock>().FirstOrDefault()?.Text : button.Content?.ToString();
            button.Style = (Style)FindResource(text == label ? "NordSelectedNavButton" : "NordNavButton");
        }
        static IEnumerable<Button> SidebarButtons(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                if (child is Button button) yield return button;
                else foreach (var nested in SidebarButtons(child)) yield return nested;
        }
    }

    private void ShowWelcome()
    {
        ClearArticlesPanel();
        ArticlesScrollViewer.Visibility = Visibility.Collapsed;
        MapContainer.Visibility = Visibility.Collapsed;
        WelcomeContainer.Visibility = Visibility.Visible;
        PageTitle.Text = "Энциклопедия";
        SetActiveSection(PageTitle.Text);
        PageDescription.Text = "База знаний Skyrim RP";
        if(_catalogDiagnostic.Length>0) PageDescription.Text+=" · "+_catalogDiagnostic;
    }

    private void Map_Click(
    object sender,
    RoutedEventArgs e)
{
    ClearArticlesPanel();
    PageTitle.Text =
        "Карта Skyrim";
    SetActiveSection(PageTitle.Text);

    PageDescription.Text =
        "Интерактивная карта Skyrim.";

    ArticlesScrollViewer.Visibility =
        Visibility.Collapsed;

    WelcomeContainer.Visibility =
        Visibility.Collapsed;

    MapContainer.Visibility =
        Visibility.Visible;

    if (_mapView == null)
    {
        _mapView =
            new MapView();

        _mapView.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        _mapView.VerticalAlignment =
            VerticalAlignment.Stretch;

        _mapView.BackRequested +=
            () =>
            {
                MapContainer.Visibility =
                    Visibility.Collapsed;

                ArticlesScrollViewer.Visibility =
                    Visibility.Visible;

                AllArticles_Click(
                    this,
                    new RoutedEventArgs());
            };

        MapContainer.Children.Add(
            _mapView);
    }
}

    private void MyArticles_Click(
        object sender,
        RoutedEventArgs e)
    {
        PageTitle.Text =
            "Мои статьи";

        PageDescription.Text =
            $"Статей: {_articles.Count}";

        ShowArticles(
            _articles);
    }

    private void Favorites_Click(
        object sender,
        RoutedEventArgs e)
    {
        PageTitle.Text =
            "Избранное";

        var favorites =
            GetAllArticles()
                .Where(
                    a =>
                        a.IsFavorite)
                .ToList();

        PageDescription.Text =
            $"Избранных статей: {favorites.Count}";

        ShowArticles(
            favorites);
    }

    private async void Notes_Click(
    object sender,
    RoutedEventArgs e)
{
    ShowMainContent();
    var allArticles =
        GetAllArticles();

    var notes =
        new List<(Article Article, ArticleNote Note)>();

    var version=_viewVersion;
    PageTitle.Text="Мои заметки"; SetActiveSection(PageTitle.Text); PageDescription.Text="Загрузка заметок…";
    try
    {
        var records=await Task.Run(()=>_database.GetAllArticleNotes(),_catalogLifetime.Token);
        if(version!=_viewVersion || _catalogLifetime.IsCancellationRequested) return;
        var byKey=allArticles.ToDictionary(a=>(a.Id,a.IsOfficial));
        foreach(var note in records) if(byKey.TryGetValue((note.ArticleId,note.ArticleIsOfficial),out var article)) notes.Add((article,note));
    }
    catch(OperationCanceledException){return;}
    catch(Exception error)
    {
        OverlayCrashDiagnostics.WriteException("Notes.Read.Failed",error);
        if(version==_viewVersion) PageDescription.Text="Не удалось прочитать заметки: "+error.Message;
        return;
    }

    PageTitle.Text =
        "Мои заметки";
    SetActiveSection(PageTitle.Text);

    PageDescription.Text =
        $"Заметок: {notes.Count}";

    ClearArticlesPanel();

    if (notes.Count == 0)
    {
        ArticlesPanel.Children.Add(CreateEmptyState(
            "Заметок пока нет",
            "Открывайте статьи и сохраняйте личные или публичные заметки — они будут собраны в этом разделе."));

        return;
    }

    foreach (var item in notes
                 .OrderByDescending(
                     x => x.Note.UpdatedAt))
    {
        var article = item.Article;
        var note = item.Note;

        var panel =
            new Border
            {
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            30,
                            30,
                            37)),
                CornerRadius =
                    new CornerRadius(8),
                Padding =
                    new Thickness(
                        15),
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10)
            };

        var stack =
            new StackPanel();

        var titleButton =
            new Button
            {
                Content =
                    $"{GetArticleSourceIcon(article)} {article.Title}",
                HorizontalContentAlignment =
                    HorizontalAlignment.Left,
                Background =
                    Brushes.Transparent,
                Foreground =
                    Brushes.White,
                BorderThickness =
                    new Thickness(0),
                FontSize = 16,
                FontWeight =
                    FontWeights.SemiBold,
                Cursor =
                    Cursors.Hand,
                Padding =
                    new Thickness(
                        0)
            };

        titleButton.Click += (_, args) =>
        {
            args.Handled = true;

            OpenArticle(
                article);
        };

        stack.Children.Add(
            titleButton);

        var typeText =
            new TextBlock
            {
                Text =
                    note.NoteType == "Public"
                        ? "Публичная заметка"
                        : "Личная заметка",
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            140,
                            140,
                            150)),
                FontSize = 12,
                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        8)
            };

        stack.Children.Add(
            typeText);

        var contentText =
            new TextBlock
            {
                Text =
                    note.Content,
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            220,
                            220,
                            225)),
                FontSize = 14,
                TextWrapping =
                    TextWrapping.Wrap,
                MaxWidth = 800
            };

        stack.Children.Add(
            contentText);

        var buttons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0)
            };

        var editButton =
            new Button
            {
                Content =
                    "✎  Редактировать",
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            45,
                            45,
                            52)),
                Foreground =
                    Brushes.White,
                BorderThickness =
                    new Thickness(0),
                Padding =
                    new Thickness(
                        12,
                        7,
                        12,
                        7),
                Cursor =
                    Cursors.Hand
            };

        editButton.Click += (_, args) =>
        {
            args.Handled = true;

            var window =
                new ArticleNotesWindow(
                    _database,
                    article)
                {
                    Owner = this
                };

            window.ShowDialog();

            Notes_Click(
                this,
                new RoutedEventArgs());
        };

        buttons.Children.Add(
            editButton);

        var openButton =
            new Button
            {
                Content =
                    "📖  Открыть статью",
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            45,
                            45,
                            52)),
                Foreground =
                    Brushes.White,
                BorderThickness =
                    new Thickness(0),
                Padding =
                    new Thickness(
                        12,
                        7,
                        12,
                        7),
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0),
                Cursor =
                    Cursors.Hand
            };

        openButton.Click += (_, args) =>
        {
            args.Handled = true;

            OpenArticle(
                article);
        };

        buttons.Children.Add(
            openButton);

        stack.Children.Add(
            buttons);

        panel.Child =
            stack;

        ArticlesPanel.Children.Add(
            panel);
    }
}

    private async void CreateArticle_Click(
        object sender,
        RoutedEventArgs e)
    {
        var editor =
            new ArticleEditorWindow(
                _database);

        editor.Owner =
            this;

        if (editor.ShowDialog() == true)
        {
            if (!await LoadArticlesAsync()) return;

            MyArticles_Click(
                this,
                new RoutedEventArgs());
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_catalogReady || _restoringCatalog) return;
        InvalidatePendingCatalog();
        var version = _viewVersion;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_catalogLifetime.Token);
        _searchCancellation = cancellation;
        var query = SearchBox.Text.Trim();
        try
        {
            if (query.Length > 0) await Task.Delay(250, cancellation.Token);
            var rows = query.Length == 0 ? GetAllArticles() :
                await Task.Run(() => _catalogReader.ReadMetadata(query, cancellation.Token), cancellation.Token);
            if (cancellation.IsCancellationRequested || version != _viewVersion) return;
            PageTitle.Text = query.Length == 0 ? "Энциклопедия" : "Поиск";
            ShowArticles(rows);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!cancellation.IsCancellationRequested && version == _viewVersion)
                PageDescription.Text = "Ошибка поиска: " + exception.Message;
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
        }
    }

        private void RecentArticles_Click(
        object sender,
        RoutedEventArgs e)
    {
        List<ArticleReference> recentReferences;
        try
        {
            recentReferences = _database.GetRecentArticles();
        }
        catch (Exception error)
        {
            OverlayCrashDiagnostics.WriteException("History.Read.Failed", error, window: this);
            PageTitle.Text = "Недавно открытые";
            ShowArticles(new List<Article>());
            PageDescription.Text = "Не удалось прочитать историю просмотров. База не изменена; подробности в диагностическом журнале.";
            _catalogDescription = PageDescription.Text;
            ArticlesPanel.Children.Clear();
            ArticlesPanel.Children.Add(CreateEmptyState("История временно недоступна", PageDescription.Text));
            return;
        }

        var recentArticles =
            new List<Article>();

        foreach (var reference in recentReferences)
        {
            var article =
                GetAllArticles()
                    .FirstOrDefault(
                        a =>
                            a.Id ==
                                reference.ArticleId &&
                            a.IsOfficial ==
                                reference.ArticleIsOfficial);

            if (article != null)
            {
                recentArticles.Add(article);
            }
        }

        PageTitle.Text =
            "Недавно открытые";

        PageDescription.Text =
            $"Недавно открытых статей: {recentArticles.Count}";

        ShowArticles(recentArticles);
    }

    private async void Metadata_Click(
        object sender,
        RoutedEventArgs e)
    {
        var window =
            new MetadataManagerWindow(
                _database)
            {
                Owner = this
            };

        window.ShowDialog();

        if (!await LoadArticlesAsync()) return;
    }

    private void Filter_Click(
        object sender,
        RoutedEventArgs e)
    {
        var window =
            new ArticleFilterWindow(
                GetAllArticles())
            {
                Owner = this
            };

        if (window.ShowDialog() != true)
        {
            return;
        }

        var articles =
            GetAllArticles();

        var filtered = articles.Where(a=>ArticleFilterRules.Matches(a,window.Request));

        var result =
            filtered.ToList();

        PageTitle.Text =
            "Фильтр";

        PageDescription.Text =
            $"Найдено статей: {result.Count}";

        ShowArticles(result);
    }
}
