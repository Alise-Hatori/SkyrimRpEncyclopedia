using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Views;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

static class GraphDataTests
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException("Graph data: " + message); }
        var folder = Path.Combine(Path.GetTempPath(), "skyrim-atlas82-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var user = Path.Combine(folder, "user.db"); var official = Path.Combine(folder, "official.db"); var knowledge = Path.Combine(folder, "knowledge.db");
        void Execute(string path, string sql)
        {
            using var c = new SqliteConnection("Data Source=" + path); c.Open();
            using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
        }
        const string legacy = """
            CREATE TABLE Articles(Id INTEGER PRIMARY KEY,Title TEXT,Category TEXT,Summary TEXT,Content TEXT,Tags TEXT,Flags TEXT,IsFavorite INTEGER,CreatedAt TEXT,UpdatedAt TEXT);
            CREATE TABLE Links(Id INTEGER PRIMARY KEY,SourceArticleId INTEGER,SourceIsOfficial INTEGER,TargetArticleId INTEGER,TargetIsOfficial INTEGER,LinkType TEXT,Description TEXT);
            INSERT INTO Articles VALUES(1,'Герой Севера','Персонаж','','ТолькоВТексте','','',0,'2026-01-01','2026-01-01');
            INSERT INTO Articles VALUES(2,'Северный двор','Локации','','ТолькоВТексте','','',0,'2026-01-01','2026-01-01');
            """;
        try
        {
            Execute(user, legacy + "INSERT INTO Links VALUES(1,1,0,1,1,'Related','Личная запись'); INSERT INTO Links VALUES(3,1000000001,1,1,0,'Related','Между источниками');");
            Execute(official, legacy + "INSERT INTO Links VALUES(1,1,1,2,1,'Related','Официальная запись');");
            Execute(knowledge, """
                CREATE TABLE Entities(Id INTEGER PRIMARY KEY,TitleRu TEXT,PrimaryCategoryRu TEXT,TypeRu TEXT,SummaryRu TEXT,ContentRu TEXT,SemanticType TEXT,NeedsReview INTEGER,CreatedAt TEXT,Url TEXT);
                CREATE TABLE Sources(Id INTEGER PRIMARY KEY,EntityId INTEGER,SourceUrl TEXT);
                CREATE TABLE Categories(Id INTEGER PRIMARY KEY,TitleRu TEXT);
                CREATE TABLE EntityCategories(EntityId INTEGER,CategoryId INTEGER);
                CREATE TABLE Relations(Id INTEGER PRIMARY KEY,SourceEntityId INTEGER,TargetEntityId INTEGER,RelationType TEXT,Evidence TEXT);
                CREATE INDEX ix_source ON Relations(SourceEntityId);
                CREATE INDEX ix_target ON Relations(TargetEntityId);
                CREATE TABLE LinkOccurrences(Id INTEGER PRIMARY KEY,ResolutionStatus TEXT,ResolvedTargetEntityId INTEGER);
                INSERT INTO LinkOccurrences VALUES(1,'Pending',9999);
                INSERT INTO Entities VALUES(1,'Герой Севера','Персонаж','Персонаж','','ТолькоВТексте','NPC',0,'2026-01-01','https://example.test/1');
                """);
            for (var id = 2; id <= 71; id++)
            {
                Execute(knowledge, $"INSERT INTO Entities VALUES({id},'Сосед {id}','Персонаж','Персонаж','','ТолькоВТексте','NPC',0,'2026-01-01','https://example.test/{id}');" +
                    $"INSERT INTO Relations VALUES({id - 1},{(id % 2 == 0 ? 1 : id)},{(id % 2 == 0 ? id : 1)},'Related','Исходная запись');");
            }
            SqliteConnection.ClearAllPools();
            var files = new[] { user, official, knowledge };
            var hashes = files.Select(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).ToArray();
            var reader = new GraphDataReader(user, official, knowledge);
            var sources = Enum.GetValues<ArticleCatalogSource>();
            var watch = Stopwatch.StartNew();
            var search = reader.Search("ГЕРОЙ", sources, 20, CancellationToken.None);
            var searchMs = watch.Elapsed.TotalMilliseconds;
            Check(search.Nodes.Count == 3 && search.Nodes.Select(n => n.Key).Distinct().Count() == 3, "IDs/names from different stores merged.");
            Check(search.Nodes.All(n => !n.Article.IsContentLoaded), "Search materialized full content.");
            Check(reader.Search("ТолькоВТексте", sources, 20, CancellationToken.None).Nodes.Count == 0, "Search not title-only.");
            Check(reader.Search("Сосед", sources, 5, CancellationToken.None) is { Limited: true, Nodes.Count: 5 }, "Search budget not reported.");
            var root = new GraphEntityKey(ArticleCatalogSource.Knowledge, 1);
            watch.Restart();
            var page = reader.ReadPage(root, new Dictionary<ArticleCatalogSource, int>(), 60, CancellationToken.None);
            var pageMs = watch.Elapsed.TotalMilliseconds;
            Check(page.Nodes.Count <= 61 && page.HasMore && page.Edges.Count > 0, "Initial neighbor limit not enforced.");
            Check(page.Edges.Any(e => e.Source == root) && page.Edges.Any(e => e.Target == root), "Direction lost.");
            Check(page.Edges.All(e => e.Type == "Related") && page.Nodes.All(n => n.Key.Id != 9999), "Invented relation/occurrence edge.");
            var state = new GraphExplorerState(); state.Reset(search.Nodes.Single(n => n.Key == root));
            Check(state.TryMerge(page), "Initial merge refused.");
            var positions = state.Positions.ToDictionary(p => p.Key, p => p.Value);
            Check(state.TryMerge(page) && state.Nodes.Count == page.Nodes.Count && state.Edges.Count == page.Edges.Count, "Retry duplicates.");
            var next = reader.ReadPage(root, state.Cursor(root), 60, CancellationToken.None);
            Check(state.TryMerge(next) && state.Nodes.Count == 72 && state.Edges.Count == 71 && !state.HasMore(root), "Cursor skip or duplicate.");
            Check(positions.All(p => state.Positions[p.Key] == p.Value), "Existing coordinates changed.");
            var localRoot = new GraphEntityKey(ArticleCatalogSource.Official, 1);
            var local = reader.ReadPage(localRoot, new Dictionary<ArticleCatalogSource, int>(), 60, CancellationToken.None);
            Check(local.Edges.Count == 2 && local.Edges.Select(e => e.Key).Distinct().Count() == 2 &&
                local.Nodes.Any(n => n.Key == new GraphEntityKey(ArticleCatalogSource.User, 1)), "Legacy store IDs or endpoint source lost.");
            var overNodes = Enumerable.Range(1000, 200).Select(id => new GraphNode(new(ArticleCatalogSource.Knowledge, id), new Article { Id = id })).ToArray();
            var countBefore = state.Nodes.Count; var cursorBefore = state.Cursor(root).ToDictionary(x => x.Key, x => x.Value);
            Check(!state.TryMerge(new(root, overNodes, [], new Dictionary<ArticleCatalogSource, int> { [ArticleCatalogSource.Knowledge] = 99999 }, false, [])) &&
                state.Nodes.Count == countBefore && cursorBefore.SequenceEqual(state.Cursor(root)), "Node cap changed partial state/cursor.");
            var overEdges = Enumerable.Range(1000, 600).Select(id => new GraphEdge(new(ArticleCatalogSource.Knowledge, id), root, root, "Related", "")).ToArray();
            Check(!state.TryMerge(new(root, [], overEdges, new Dictionary<ArticleCatalogSource, int>(), false, [])), "Edge cap exceeded.");
            var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { reader.ReadPage(root, new Dictionary<ArticleCatalogSource, int>(), 60, canceled.Token); throw new InvalidOperationException("Cancellation ignored"); }
            catch (OperationCanceledException) { checks++; }
            using (var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(knowledge))
            {
                connection.Open();
                using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
                connection.CreateFunction<int, int>("cancel_probe", value => { started.Set(); release.Wait(TimeSpan.FromSeconds(3)); return value; });
                using var command = connection.CreateCommand(); command.CommandText = "SELECT cancel_probe(7)";
                var operation = Task.Run(() => command.ExecuteScalar());
                if (!started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException();
                command.Cancel(); release.Set();
                Check(Convert.ToInt32(operation.GetAwaiter().GetResult()) == 7, "Provider Cancel behavior changed; revisit cancellation contract.");
            }
            Check(reader.Search("Герой", [ArticleCatalogSource.User], 20, CancellationToken.None).Nodes.Single().Key.SourceKind == ArticleCatalogSource.User,
                "Source search filter ignored.");
            UiTests(reader, search.Nodes.Single(n => n.Key == root), Check);
            SqliteConnection.ClearAllPools();
            Check(files.Select(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).SequenceEqual(hashes), "Read-only fixture hashes changed.");
            Console.WriteLine($"Graph data OK: {checks} checks; title search, three ID namespaces, directed/store-scoped edges, pagination, caps, stable coordinates, stale/canceled requests and read-only hashes.");
            Console.WriteLine($"Graph synthetic measurements: search={searchMs:F1} ms; page60={pageMs:F1} ms.");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }

    private static void UiTests(IGraphDataReader reader, GraphNode root, Action<bool, string> check)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                void Finish(Task task)
                {
                    var frame = new DispatcherFrame();
                    var timeout = Stopwatch.StartNew();
                    var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(5), DispatcherPriority.Background, (_, _) =>
                    { if (task.IsCompleted || timeout.Elapsed > TimeSpan.FromSeconds(10)) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
                    timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
                    if (!task.IsCompleted) throw new TimeoutException("UI request did not finish");
                    task.GetAwaiter().GetResult();
                }
                var view = new GraphView(reader);
                Finish(view.SelectCenterAsync(root));
                check(view.Explorer.Nodes.Count == 61, "Public view did not use 60-neighbor page.");
                var coords = view.Explorer.Positions.ToDictionary(p => p.Key, p => p.Value);
                var filter = (CheckBox)view.FindName("ShowCharactersCheckBox");
                filter.IsChecked = false; filter.IsChecked = true;
                check(coords.All(p => view.Explorer.Positions[p.Key] == p.Value), "Filter changed coordinates.");
                Article? opened = null; view.ArticleSelected += a => opened = a;
                ((Button)view.FindName("OpenArticleButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(opened is { IsMvp: true, Id: 1000000001 }, "Wrong article passed to navigation.");
                Finish(view.ExpandAsync(root.Key));
                check(view.Explorer.Nodes.Count == 72, "View expansion lost data.");
                var status = ((TextBlock)view.FindName("StatusText")).Text;
                check(status.Contains("применение"), "Apply timing missing.");
                Console.WriteLine("Graph UI apply measurement (unshown control): " + status);
                view.Measure(new Size(1280, 800)); view.Arrange(new Rect(0, 0, 1280, 800)); view.UpdateLayout();
                check(((Canvas)view.FindName("GraphCanvas")).Children.Count <= GraphExplorerState.MaxNodes + 1,
                    "Visual tree exceeds bounded nodes plus one edge layer.");
                var existing = ((Canvas)view.FindName("GraphCanvas")).Children.Cast<UIElement>().ToArray();
                filter.IsChecked = false; filter.IsChecked = true;
                check(existing.SequenceEqual(((Canvas)view.FindName("GraphCanvas")).Children.Cast<UIElement>()), "Filters recreated visuals.");

                var gate = new DelayedReader();
                var delayed = new GraphView(gate);
                var old = delayed.SearchAsync("old");
                if (!gate.Started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException();
                var latest = delayed.SearchAsync("new");
                gate.Release.Set();
                Finish(Task.WhenAll(old, latest));
                var results = (ListBox)delayed.FindName("SearchResults");
                check(results.Items.Count == 1 && results.Items[0].ToString()!.Contains("new"), "Stale search replaced latest.");
                var pending = delayed.SearchAsync("leave");
                delayed.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Finish(pending);
                check(results.Items.Count == 0, "Unloaded view applied data.");
                var pageGate = new DelayedPages(); var switching = new GraphView(pageGate);
                var oldRoot = new GraphNode(new(ArticleCatalogSource.User, 101), new Article { Id = 101, Title = "old root" });
                var newRoot = new GraphNode(new(ArticleCatalogSource.User, 102), new Article { Id = 102, Title = "new root" });
                var oldPage = switching.SelectCenterAsync(oldRoot);
                if (!pageGate.Started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException();
                var newPage = switching.SelectCenterAsync(newRoot);
                pageGate.Release.Set(); Finish(Task.WhenAll(oldPage, newPage));
                check(switching.Explorer.Center == newRoot.Key && !switching.Explorer.Nodes.ContainsKey(oldRoot.Key), "Stale page/camera applied to new center.");
                var broken = new GraphView(new BrokenReader());
                Finish(broken.SelectCenterAsync(newRoot));
                check(((TextBlock)broken.FindName("StatusText")).Text.Contains("контрольный отказ") && broken.Explorer.Cursor(newRoot.Key).Count == 0,
                    "Source error hidden or cursor advanced.");
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw new InvalidOperationException("Graph interaction failure", failure);
    }
    private sealed class DelayedReader : IGraphDataReader
    {
        public ManualResetEventSlim Started { get; } = new(); public ManualResetEventSlim Release { get; } = new();
        public GraphSearchResult Search(string query, IReadOnlyCollection<ArticleCatalogSource> sources, int limit, CancellationToken token)
        {
            if (query == "old") { Started.Set(); Release.Wait(); } // Deliberately ignores cancellation to verify epoch guard.
            return new([new(new(ArticleCatalogSource.User, 1), new Article { Id = 1, Title = query })], false, []);
        }
        public GraphPage ReadPage(GraphEntityKey root, IReadOnlyDictionary<ArticleCatalogSource, int> cursor, int neighbors, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class DelayedPages : IGraphDataReader
    {
        public ManualResetEventSlim Started { get; } = new(); public ManualResetEventSlim Release { get; } = new();
        public GraphSearchResult Search(string query, IReadOnlyCollection<ArticleCatalogSource> sources, int limit, CancellationToken token) => new([], false, []);
        public GraphPage ReadPage(GraphEntityKey root, IReadOnlyDictionary<ArticleCatalogSource, int> cursor, int neighbors, CancellationToken token)
        {
            if (root.Id == 101) { Started.Set(); Release.Wait(); }
            return new(root, [new(root, new Article { Id = root.Id, Title = "page " + root.Id })], [],
                new Dictionary<ArticleCatalogSource, int> { [root.SourceKind] = 1 }, false, []);
        }
    }
    private sealed class BrokenReader : IGraphDataReader
    {
        public GraphSearchResult Search(string query, IReadOnlyCollection<ArticleCatalogSource> sources, int limit, CancellationToken token) => throw new InvalidOperationException("контрольный отказ");
        public GraphPage ReadPage(GraphEntityKey root, IReadOnlyDictionary<ArticleCatalogSource, int> cursor, int neighbors, CancellationToken token) => throw new InvalidOperationException("контрольный отказ");
    }

    public static void MeasureReal(string path)
    {
        var reader = new GraphDataReader(path + ".absent-user", path + ".absent-official", path);
        var watch = Stopwatch.StartNew();
        var search = reader.Search("Вайтран", [ArticleCatalogSource.Knowledge], 20, CancellationToken.None);
        var searchMs = watch.Elapsed.TotalMilliseconds;
        if (search.Nodes.Count == 0) throw new InvalidOperationException("Real graph search returned no Whiterun fixture entries.");
        var root = search.Nodes.FirstOrDefault(n => n.Article.Title == "Вайтран") ?? search.Nodes[0];
        watch.Restart();
        var page = reader.ReadPage(root.Key, new Dictionary<ArticleCatalogSource, int>(), 60, CancellationToken.None);
        var readMs = watch.Elapsed.TotalMilliseconds;
        var state = new GraphExplorerState(); state.Reset(root);
        watch.Restart(); state.TryMerge(page); var mergeMs = watch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Graph real fixture: root={root.Article.Title}, search={searchMs:F1} ms, page60={readMs:F1} ms, modelApply={mergeMs:F1} ms, nodes={state.Nodes.Count}, edges={state.Edges.Count}, more={page.HasMore}; read-only, full content not loaded.");
    }
}
