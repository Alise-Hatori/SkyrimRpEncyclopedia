using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

internal enum AtlasPresentationState { Initial, DataUnavailable, Loading, Empty, Error }

/// <summary>Bounded asynchronous explorer. Data readers never execute on the UI thread.</summary>
public partial class GraphView : UserControl
{
    private bool _ready;
    private bool _filtersRequested = true;
    private bool _detailsRequested = true;
    private Article? _selectedArticle;
    private readonly IGraphDataReader? _reader;
    private readonly SemaphoreSlim _readSlot = new(1, 1);
    private CancellationTokenSource? _request;
    private long _version;
    private bool _active = true;
    private readonly Dictionary<GraphEntityKey, Border> _visualNodes = new();
    private readonly GraphEdgeLayer _edgeLayer = new();
    internal GraphExplorerState Explorer { get; } = new();
    private GraphEntityKey? _selectedKey;
    private IReadOnlyList<GraphNode> _searchNodes = Array.Empty<GraphNode>();
    public event Action<Article>? ArticleSelected;
    public event Action? BackRequested;

    public GraphView() : this((IGraphDataReader?)null) { }
    internal GraphView(IGraphDataReader? reader, IGraphMapReader? mapReader = null)
    {
        _reader = reader;
        _mapReader = mapReader;
        InitializeComponent();
        GraphCanvas.Children.Add(_edgeLayer);
        GraphViewport.Children.Add(_mapLayer);
        BatchBox.ItemsSource = new[] { 20, 40, 60 };
        BatchBox.SelectedItem = 60;
        Loaded += (_, _) => _active = true;
        Unloaded += (_, _) => { _active = false; CancelRequests(); };
        _ready = true;
        ShowState(AtlasPresentationState.Initial);
        UpdatePanels();
    }

    public GraphView(DatabaseService database, OfficialDatabaseService officialDatabase,
        MvpKnowledgeDatabaseService mvpDatabase) : this(database is null || officialDatabase is null || mvpDatabase is null ? null :
            new GraphDataReader(database.DatabasePath, officialDatabase.DatabasePath, mvpDatabase.DatabasePath))
    { if (mvpDatabase is not null) _mapReader = new GraphMapReader(mvpDatabase.DatabasePath); }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync(SearchBox.Text);
    private async void Search_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SearchAsync(SearchBox.Text);
    }
    private async void Search_Changed(object sender, TextChangedEventArgs e)
    { if (_ready) await SearchAsync(SearchBox.Text, true); }
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_fullMode) { _ = LoadFullMapAsync(); return; }
        SearchBox.Clear();
        CancelRequests();
        Explorer.Clear(); _selectedKey = null; _searchNodes = Array.Empty<GraphNode>();
        RenderGraph();
        SearchResults.ItemsSource = null;
        SetSelectedArticle(null);
        ShowState(AtlasPresentationState.Initial);
    }
    private void Back_Click(object sender, RoutedEventArgs e) { CancelRequests(); BackRequested?.Invoke(); }
    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (_fullMode) { _ = ApplyFullFiltersAsync(); return; }
        CancelRequests();
        StateProgress.Visibility = Visibility.Collapsed;
        SearchResults.ItemsSource = _searchNodes.Where(Visible).Select(n => new SearchEntry(n)).ToArray();
        if (_reader is null) ShowState(AtlasPresentationState.DataUnavailable);
        else { RenderGraph(); StatusText.Text = "Фильтры применены к загруженным данным без SQL."; }
    }
    private void Filters_Click(object sender, RoutedEventArgs e)
    {
        _filtersRequested = FiltersPanel.Visibility != Visibility.Visible;
        UpdatePanels(_filtersRequested ? "filters" : null);
    }
    private void Details_Click(object sender, RoutedEventArgs e)
    {
        _detailsRequested = DetailsPanel.Visibility != Visibility.Visible;
        UpdatePanels(_detailsRequested ? "details" : null);
    }
    private void View_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_ready) UpdatePanels();
    }
    private void UpdatePanels(string? preferred = null)
    {
        var compact = ActualWidth > 0 && ActualWidth < 980;
        StateContent.Margin = new Thickness(0, compact ? 35 : 100, 0, 25);
        FiltersPanel.Visibility = !compact && _filtersRequested ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = !compact && _detailsRequested ? Visibility.Visible : Visibility.Collapsed;
        if (compact)
        {
            // Alternative drawers overlay the map rather than taking away its width.
            if (preferred == "filters" && _filtersRequested)
            {
                FiltersPanel.Visibility = Visibility.Visible;
                Grid.SetColumn(FiltersPanel, 1);
                FiltersPanel.HorizontalAlignment = HorizontalAlignment.Left;
            }
            if (preferred == "details" && _detailsRequested)
            {
                DetailsPanel.Visibility = Visibility.Visible;
                Grid.SetColumn(DetailsPanel, 1);
                DetailsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }
        else
        {
            Grid.SetColumn(FiltersPanel, 0);
            Grid.SetColumn(DetailsPanel, 2);
            FiltersPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            DetailsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }

    private void CancelRequests() { _version++; _request?.Cancel(); StateProgress.Visibility = Visibility.Collapsed; CancelButton.IsEnabled = false; }
    private IReadOnlyCollection<ArticleCatalogSource> Sources() => new[]
    {
        (ArticleCatalogSource.Official, ShowOfficialCheckBox.IsChecked == true),
        (ArticleCatalogSource.Knowledge, ShowMvpCheckBox.IsChecked == true),
        (ArticleCatalogSource.User, ShowPersonalCheckBox.IsChecked == true)
    }.Where(p => p.Item2).Select(p => p.Item1).ToArray();

    internal async Task SearchAsync(string query, bool debounce = false)
    {
        if (_fullMode) { SearchFullMap(query); return; }
        CancelRequests();
        if (_reader is null) { ShowState(AtlasPresentationState.DataUnavailable); return; }
        var version = _version; var sources = Sources();
        var cancellation = new CancellationTokenSource(); _request = cancellation;
        try
        {
            SearchResults.ItemsSource = null;
            _searchNodes = Array.Empty<GraphNode>();
            if (string.IsNullOrWhiteSpace(query)) { if (Explorer.Nodes.Count == 0) ShowState(AtlasPresentationState.Initial); return; }
            if (debounce) await Task.Delay(250, cancellation.Token);
            ShowState(AtlasPresentationState.Loading);
            var watch = Stopwatch.StartNew();
            await _readSlot.WaitAsync(cancellation.Token);
            GraphSearchResult result;
            try { result = await Task.Run(() => _reader.Search(query.Trim(), sources, 20, cancellation.Token), cancellation.Token); }
            finally { _readSlot.Release(); }
            if (!_active || cancellation.IsCancellationRequested || version != _version) return;
            _searchNodes = result.Nodes;
            SearchResults.ItemsSource = result.Nodes.Where(Visible).Select(n => new SearchEntry(n)).ToArray();
            StateProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Поиск: {result.Nodes.Count} · {watch.Elapsed.TotalMilliseconds:F0} мс" +
                (result.Limited ? " · показаны первые 20, уточните название" : "") + " " + string.Join(" ", result.Warnings);
            if (result.Nodes.Count == 0) { ShowState(result.Warnings.Count > 0 ? AtlasPresentationState.Error : AtlasPresentationState.Empty,
                result.Warnings.Count > 0 ? string.Join(" ", result.Warnings) : null); StateDescription.Text += " Поиск не дал результатов."; }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (_active && version == _version) ShowState(AtlasPresentationState.Error, e.Message); }
        finally { if (ReferenceEquals(_request, cancellation)) _request = null; cancellation.Dispose(); }
    }
    private sealed record SearchEntry(GraphNode Node)
    { public override string ToString() => $"{Node.Article.Title} · {Node.Key.SourceKind} · #{Node.Key.Id}"; }
    private async void SearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (_fullMode && SearchResults.SelectedItem is MapSearchEntry mapEntry)
        {
            var selecting = SelectMapNodeAsync(mapEntry.Node.Key); var epoch = _version; await selecting;
            if (!_active || !_fullMode || epoch != _version || _fullDrawing is null || !_mapLayer.VisibleKeys.Contains(mapEntry.Node.Key)) return;
            Camera.Center(_fullDrawing.Positions[mapEntry.Node.Key], ViewportSize); ApplyCamera();
        }
        else if (!_fullMode && SearchResults.SelectedItem is SearchEntry entry) await SelectCenterAsync(entry.Node);
    }
    internal async Task SelectCenterAsync(GraphNode node)
    {
        CancelRequests();
        Explorer.Reset(node); _selectedKey = node.Key; SetSelectedArticle(node.Article);
        RenderGraph();
        var loading = ExpandAsync(node.Key); var epoch = _version;
        await loading;
        GraphViewport.UpdateLayout();
        if (!_active || epoch != _version || Explorer.Center != node.Key || !Explorer.Positions.TryGetValue(node.Key, out var p)) return;
        Camera.Center(new(p.X + 80, p.Y + 32), ViewportSize); ApplyCamera();
    }
    private async void CenterSelected_Click(object sender, RoutedEventArgs e)
    { if (_selectedKey is GraphEntityKey key && Explorer.Nodes.TryGetValue(key, out var node)) await SelectCenterAsync(node); }
    private async void Expand_Click(object sender, RoutedEventArgs e)
    { if (_selectedKey is GraphEntityKey key) await ExpandAsync(key); }
    internal async Task ExpandAsync(GraphEntityKey key)
    {
        if (_reader is null) { ShowState(AtlasPresentationState.DataUnavailable); return; }
        if (!Explorer.HasMore(key)) { StatusText.Text = "Все доступные порции этой сущности уже загружены."; return; }
        CancelRequests(); var version = _version;
        var cancellation = new CancellationTokenSource(); _request = cancellation;
        var cursor = Explorer.Cursor(key).ToDictionary(p => p.Key, p => p.Value);
        var batch = BatchBox.SelectedItem is int count ? count : 60;
        try
        {
            ShowState(AtlasPresentationState.Loading);
            var watch = Stopwatch.StartNew();
            await _readSlot.WaitAsync(cancellation.Token);
            GraphPage page;
            try { page = await Task.Run(() => _reader.ReadPage(key, cursor, batch, cancellation.Token), cancellation.Token); }
            finally { _readSlot.Release(); }
            if (!_active || cancellation.IsCancellationRequested || version != _version) return;
            var readMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
            if (!Explorer.TryMerge(page))
            { StateProgress.Visibility = Visibility.Collapsed; StatusText.Text = "Бюджет 200 узлов / 600 связей достигнут. Выберите меньшую порцию или замените центральный узел. Курсор не изменён."; return; }
            RenderGraph();
            StateProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Окружение: {readMs:F0} мс · применение: {watch.Elapsed.TotalMilliseconds:F0} мс · " +
                (page.Warnings.Count > 0 ? "окружение проверено частично; для повторной проверки замените центр" :
                    page.HasMore ? "есть дополнительные порции" : "доступные порции этой сущности исчерпаны") + " " + string.Join(" ", page.Warnings);
            if (page.Edges.Count == 0 && Explorer.Edges.Count == 0) StatusText.Text += " · Связи не найдены.";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (_active && version == _version) ShowState(AtlasPresentationState.Error, e.Message); }
        finally { if (ReferenceEquals(_request, cancellation)) _request = null; cancellation.Dispose(); }
    }
    private bool Visible(GraphNode node)
    {
        if (!Sources().Contains(node.Key.SourceKind)) return false;
        return TypeVisible(ArticleGraphTypeClassifier.Classify(node.Article));
    }
    private bool TypeVisible(ArticleGraphType type)
    {
        return type switch
        {
            ArticleGraphType.Character => ShowCharactersCheckBox.IsChecked == true,
            ArticleGraphType.Place => ShowPlacesCheckBox.IsChecked == true,
            ArticleGraphType.Faction => ShowFactionsCheckBox.IsChecked == true,
            ArticleGraphType.Quest => ShowQuestsCheckBox.IsChecked == true,
            ArticleGraphType.Item => ShowItemsCheckBox.IsChecked == true,
            ArticleGraphType.Creature => ShowCreaturesCheckBox.IsChecked == true,
            ArticleGraphType.Race => ShowRacesCheckBox.IsChecked == true,
            ArticleGraphType.Spell => ShowSpellsCheckBox.IsChecked == true,
            ArticleGraphType.Religion => ShowReligionCheckBox.IsChecked == true,
            _ => ShowOtherCheckBox.IsChecked == true
        };
    }
    private void RenderGraph()
    {
        foreach (var obsolete in _visualNodes.Keys.Where(k => !Explorer.Nodes.ContainsKey(k)).ToArray())
        { GraphCanvas.Children.Remove(_visualNodes[obsolete]); _visualNodes.Remove(obsolete); }
        var visible = Explorer.Nodes.Values.Where(Visible).Select(n => n.Key).ToHashSet();
        foreach (var node in Explorer.Nodes.Values)
        {
            if (!_visualNodes.TryGetValue(node.Key, out var border))
            {
                border = new Border { Width = 160, Height = 64, Padding = new Thickness(8), CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromRgb(24, 38, 49)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = node.Article.Title, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 48 },
                    ToolTip = node.Article.Title + "\n" + node.Key.SourceKind + " · " + node.Article.Category };
                var key = node.Key;
                border.MouseLeftButtonDown += (_, e) => { _selectedKey = key; SetSelectedArticle(Explorer.Nodes[key].Article); RenderGraph(); e.Handled = true; };
                _visualNodes[key] = border; GraphCanvas.Children.Add(border);
            }
            border.Visibility = visible.Contains(node.Key) ? Visibility.Visible : Visibility.Collapsed;
            border.BorderBrush = node.Key == _selectedKey ? Brushes.Goldenrod : Brushes.SlateGray;
            var p = Explorer.Positions[node.Key]; Canvas.SetLeft(border, p.X); Canvas.SetTop(border, p.Y);
        }
        var edges = Explorer.Edges.Values.Where(e => visible.Contains(e.Source) && visible.Contains(e.Target)).ToArray();
        _edgeLayer.Edges = edges; _edgeLayer.Positions = Explorer.Positions; _edgeLayer.InvalidateVisual();
        GraphCanvas.Width = Math.Max(1100, Explorer.Positions.Values.Select(p => p.X + 190).DefaultIfEmpty(1100).Max());
        GraphCanvas.Height = Math.Max(800, Explorer.Positions.Values.Select(p => p.Y + 110).DefaultIfEmpty(800).Max());
        _edgeLayer.Width = GraphCanvas.Width; _edgeLayer.Height = GraphCanvas.Height;
        GraphViewport.Visibility = Explorer.Nodes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        StateScroller.Visibility = Explorer.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"Показано: {visible.Count} узлов / {edges.Length} связей · загружено: {Explorer.Nodes.Count} / {Explorer.Edges.Count}";
        LimitText.Text = "Бюджет: 200 узлов / 600 связей. Стрелка — направление записи, не причина или влияние.";
        ExpandButton.IsEnabled = _selectedKey is GraphEntityKey selected && Explorer.Nodes.ContainsKey(selected) && Explorer.HasMore(selected);
        CenterSelectedButton.IsEnabled = _selectedKey is GraphEntityKey current && Explorer.Nodes.ContainsKey(current);
        if (_selectedKey is GraphEntityKey selectedKey && Explorer.Nodes.TryGetValue(selectedKey, out var selectedNode))
        {
            SetSelectedArticle(selectedNode.Article);
            if (!visible.Contains(selectedKey)) OpenArticleButton.IsEnabled = ExpandButton.IsEnabled = CenterSelectedButton.IsEnabled = false;
        }
        if (Explorer.Nodes.Count > 0 && visible.Count == 0)
        {
            GraphViewport.Visibility = Visibility.Collapsed; StateScroller.Visibility = Visibility.Visible;
            ShowState(AtlasPresentationState.Empty); StateDescription.Text = "Все загруженные узлы скрыты фильтрами. Данные и координаты сохранены.";
        }
        ApplyCamera();
    }

    internal void ShowState(AtlasPresentationState state, string? diagnostic = null)
    {
        StateProgress.Visibility = state == AtlasPresentationState.Loading ? Visibility.Visible : Visibility.Collapsed;
        StateTitle.Text = state switch
        {
            AtlasPresentationState.DataUnavailable => "Источники ещё не подключены",
            AtlasPresentationState.Loading => "Открываем путь к знаниям",
            AtlasPresentationState.Empty => "В этом окружении пока нет узлов",
            AtlasPresentationState.Error => "Не удалось получить данные",
            _ => "Выберите начало исследования"
        };
        StateDescription.Text = state switch
        {
            AtlasPresentationState.DataUnavailable => "Источник не подключён: проверьте пути баз. Запросы к базе не выполняются, результаты не имитируются.",
            AtlasPresentationState.Loading => _fullMode ? "Читаем метаданные и отношения Knowledge DB, затем готовим карту. Можно отменить загрузку или вернуться в каталог." :
                "Ожидаем ограниченное окружение выбранной сущности. Вы можете вернуться в каталог.",
            AtlasPresentationState.Empty => "Для этого запроса или окружения данных не найдено. Неразрешённые ссылки не превращаются в рёбра.",
            AtlasPresentationState.Error => diagnostic ?? "Источник недоступен. Данные и базы не изменялись.",
            _ => "Начните с названия статьи. Атлас раскроет её ближайшее окружение, а не весь корпус сразу."
        };
        StatusText.Text = state switch
        {
            AtlasPresentationState.Error => "Ошибка источника: " + diagnostic,
            AtlasPresentationState.Loading => "Ожидание данных",
            _ => "Данные ещё не загружены"
        };
    }

    /// <summary>Supplied metadata only; never materializes Content or reads relations.</summary>
    internal void SetSelectedArticle(Article? article)
    {
        _selectedArticle = article;
        SelectedTitle.Text = article?.Title ?? "Сущность не выбрана";
        SelectedCategory.Text = article?.Category ?? "Тип и категории появятся после выбора";
        SelectedSource.Text = article is null ? "Источник не выбран" :
            article.IsMvp ? "Knowledge DB · только чтение" : article.IsOfficial ? "Официальный архив" : "Личные материалы";
        SelectionNote.Text = article is null
            ? "Щелчок выбирает узел. Статья открывается отдельным действием."
            : "Показаны метаданные и загруженные связи. При открытии статьи из атласа передаётся только загруженная часть связей. Отношения не доказывают мотивы или союз.";
        OpenArticleButton.IsEnabled = article is not null;
        if (article is null) RelationNotes.Text = "";
        if (article is not null)
        {
            var key = GraphEntityKey.FromArticle(article);
            RelationNotes.Text = string.Join("\n", LoadedEdges(key).Take(20)
                .Select(e => $"{(e.Source == key ? "Исходящая" : "Входящая")}: {e.Type} · {e.Key.Store} #{e.Key.Id}\n{e.Description}"));
        }
    }
    private void OpenArticle_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedArticle is not null) ArticleSelected?.Invoke(_selectedArticle);
    }
    internal List<ArticleLink> GetLoadedLinks(Article article)
    {
        var key = GraphEntityKey.FromArticle(article);
        static int UiId(GraphEntityKey k) => k.SourceKind == ArticleCatalogSource.Knowledge ? MvpKnowledgeDatabaseService.ToArticleId(k.Id) : k.Id;
        return LoadedEdges(key).Select(e => new ArticleLink
        {
            Id = e.Key.Store == ArticleCatalogSource.Knowledge ? MvpKnowledgeDatabaseService.ToLinkId(e.Key.Id) : e.Key.Id,
            SourceArticleId = UiId(e.Source), SourceIsOfficial = e.Source.SourceKind != ArticleCatalogSource.User,
            TargetArticleId = UiId(e.Target), TargetIsOfficial = e.Target.SourceKind != ArticleCatalogSource.User,
            LinkType = e.Type, Description = e.Description
        }).ToList();
    }
}
