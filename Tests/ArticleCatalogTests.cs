using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Views;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

static class ArticleCatalogTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "skyrim-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var user = Path.Combine(directory, "user.db");
        var official = Path.Combine(directory, "official.db");
        var knowledge = Path.Combine(directory, "knowledge.db");
        try
        {
            foreach (var path in new[] { user, official })
            {
                using var connection = new SqliteConnection("Data Source=" + path);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Articles(Id INTEGER PRIMARY KEY,Title TEXT,Category TEXT,Summary TEXT,Content TEXT,Tags TEXT,Flags TEXT,IsFavorite INTEGER,CreatedAt TEXT,UpdatedAt TEXT);
                    INSERT INTO Articles VALUES(1,'Альфа','Категория','Кратко','Спрятанная фраза','ярлык','флаг',1,'2026-10-01','2026-10-01');
                    INSERT INTO Articles VALUES(2,'Бета','Другая','','Другой текст','','',0,'2026-10-02','2026-10-02');
                    """;
                command.ExecuteNonQuery();
            }
            using (var connection = new SqliteConnection("Data Source=" + knowledge))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Entities(Id INTEGER PRIMARY KEY,TitleRu TEXT,PrimaryCategoryRu TEXT,TypeRu TEXT,SummaryRu TEXT,ContentRu TEXT,SemanticType TEXT,NeedsReview INTEGER,CreatedAt TEXT,Url TEXT);
                    CREATE TABLE Sources(Id INTEGER PRIMARY KEY,EntityId INTEGER,SourceUrl TEXT);
                    CREATE TABLE Categories(Id INTEGER PRIMARY KEY,TitleRu TEXT);
                    CREATE TABLE EntityCategories(EntityId INTEGER,CategoryId INTEGER);
                    INSERT INTO Entities VALUES(1,'Альфа','Категория','Персонаж','Кратко','Спрятанная фраза','NPC',1,'2026-10-01','https://example.test/entity');
                    INSERT INTO Entities VALUES(2,'Бета','Другая','Место','','Другой текст','Place',0,'2026-10-02','https://example.test/beta');
                    INSERT INTO Sources VALUES(1,1,'https://example.test/source');
                    INSERT INTO Categories VALUES(1,'Дополнительная');
                    INSERT INTO EntityCategories VALUES(1,1);
                    """;
                command.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();
            var before = new[] { user, official, knowledge }.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
            var catalog = new ArticleCatalogReader(user, official, knowledge);
            var rows = catalog.ReadMetadata();
            Require(rows.Count == 6 && rows.All(a => !a.IsContentLoaded), "Catalog loaded content or lost a source.");
            Require(rows.Select(a => a.Title).SequenceEqual(new[] { "Бета", "Альфа", "Альфа", "Бета", "Альфа", "Бета" }), "Source concatenation or sorting changed.");
            foreach (var source in new[] { ArticleCatalogSource.Official, ArticleCatalogSource.Knowledge, ArticleCatalogSource.User })
            {
                var pages = Enumerable.Range(0, 2).SelectMany(offset => catalog.ReadPage(source, offset, 1)).ToArray();
                Require(pages.Length == 2 && pages.Select(a => a.Id).Distinct().Count() == 2 && catalog.ReadPage(source, 2, 1).Count == 0,
                    "Pagination skipped or duplicated rows.");
            }
            var sameId = rows.Where(a => a.Title == "Альфа").ToArray();
            Require(sameId.Select(a => (a.Id, a.IsOfficial, a.IsMvp)).Distinct().Count() == 3, "Source identities collided.");
            Require(sameId.All(a => a.Content == "Спрятанная фраза" && a.IsContentLoaded), "Deferred content came from the wrong source.");
            var mvp = sameId.Single(a => a.IsMvp);
            Require(mvp.SourceUrl == "https://example.test/source" && mvp.AdditionalCategories.Single() == "Дополнительная" && mvp.Flags == "Требует проверки", "Knowledge metadata lost provenance or flags.");
            foreach (var search in new[] { "СПРЯТАННАЯ ФРАЗА", "альфа", "КРАТКО", "КАТЕГОРИЯ" })
                Require(catalog.ReadMetadata(search).Count == 3, "Search lost a field or Cyrillic case matching.");
            Require(catalog.ReadMetadata("NPC").Single().IsMvp && catalog.ReadMetadata("ярлык").Count == 2 &&
                catalog.ReadMetadata("Требует проверки").Single().IsMvp && catalog.ReadMetadata("Несуществующее").Count == 0,
                "Type, tag, flag or empty search regressed.");
            var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { catalog.ReadMetadata(cancellationToken: cancellation.Token); throw new InvalidOperationException("Cancelled request completed."); }
            catch (OperationCanceledException) { }
            Require(new ArticleCatalogReader(user, official, null).ReadMetadata().Count == 4, "Absent optional Knowledge DB is not supported.");
            try { new ArticleCatalogReader("missing.db", official, knowledge).ReadMetadata(); throw new InvalidOperationException("Missing source silently accepted."); }
            catch (FileNotFoundException) { }
            SqliteConnection.ClearAllPools();
            Require(new[] { user, official, knowledge }.Select((path,index) => before[index].SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)))).All(x => x), "Catalog changed database bytes.");
            Console.WriteLine("Article catalog OK: three sources, metadata-only pages, deferred content, SQL content search, cancellation and unchanged DBs.");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(directory, recursive: true); }
        VerifyVirtualization();
    }

    private static void VerifyVirtualization()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Actual catalog control; no Window, UI automation, application services or real databases.
                var view = new ArticleCatalogView();
                var memoryBefore = GC.GetTotalMemory(false);
                var items = Enumerable.Range(0, 10000).Select(i => new Article { Id=i, Title="Статья " + i, Summary="Краткое описание", IsOfficial=true }).ToArray();
                var layoutWatch = Stopwatch.StartNew();
                view.ShowArticles(items);
                view.Measure(new Size(900,600)); view.Arrange(new Rect(0,0,900,600)); view.UpdateLayout();
                var firstLayoutMs = layoutWatch.Elapsed.TotalMilliseconds;
                var list = (ListBox)view.FindName("CatalogList");
                var containers = Enumerable.Range(0,items.Length).Count(i => list.ItemContainerGenerator.ContainerFromIndex(i) is not null);
                Require(containers > 0 && containers < 200, "Actual catalog realized all items instead of virtualizing.");
                list.ScrollIntoView(items[^1]);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                view.UpdateLayout();
                var afterScroll = Enumerable.Range(0,items.Length).Count(i => list.ItemContainerGenerator.ContainerFromIndex(i) is not null);
                Require(afterScroll > 0 && afterScroll < 200 && list.ItemContainerGenerator.ContainerFromIndex(items.Length-1) is not null,
                    "Virtualized catalog cannot reach the last article.");
                var memoryAfterScroll = GC.GetTotalMemory(false);
                layoutWatch.Restart(); view.ShowArticles(items); view.Measure(new Size(900,600)); view.Arrange(new Rect(0,0,900,600)); view.UpdateLayout();
                Console.WriteLine($"Catalog layout test OK: 10000 items, realized={containers}, after last-item scroll={afterScroll}, firstLayoutMs={firstLayoutMs:F1}, repeatedLayoutMs={layoutWatch.Elapsed.TotalMilliseconds:F1}, managedMemoryBefore={memoryBefore}, afterScroll={memoryAfterScroll}; no visible window.");
            }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw new InvalidOperationException("Catalog layout test failed.", failure);
    }
    public static void MeasureKnowledge(string path)
    {
        var reader = new ArticleCatalogReader("unused-user", "unused-official", path);
        var watch = Stopwatch.StartNew();
        var rows = reader.ReadSourceMetadata(ArticleCatalogSource.Knowledge);
        var metadataMs=watch.Elapsed.TotalMilliseconds;
        Require(rows.All(a=>!a.IsContentLoaded), "Measurement materialized full content.");
        watch.Restart(); var results=reader.ReadPage(ArticleCatalogSource.Knowledge,0,256,"ДРАКОН"); var searchMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart(); var content=rows[0].Content; var contentMs=watch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Knowledge catalog single-run measurement: metadataRows={rows.Count}, metadataMs={metadataMs:F1}, searchPageRows={results.Count}, searchMs={searchMs:F1}, oneContentChars={content.Length}, oneContentMs={contentMs:F1}.");
    }
    private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
