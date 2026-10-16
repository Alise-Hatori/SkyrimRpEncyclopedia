using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Services;

static class RecentHistoryTests
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Recent history: " + message);
        }
        var folder = Path.Combine(Path.GetTempPath(), "skyrim-history94-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        void Execute(string path, string sql)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        try
        {
            foreach (var compositeKey in new[] { true, false })
            {
                var path = Path.Combine(folder, compositeKey ? "existing-shape.db" : "new-shape.db");
                if (compositeKey)
                {
                    // Exact history shape observed read-only in the supported user.db.
                    // Articles is the minimum identity needed by the existing service preflight.
                    Execute(path, """
                        CREATE TABLE Articles (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL,
                            Category TEXT NOT NULL, Content TEXT);
                        CREATE TABLE ViewingHistory (
                            ArticleId INTEGER NOT NULL,
                            ArticleIsOfficial INTEGER NOT NULL DEFAULT 0,
                            OpenedAt TEXT NOT NULL,
                            ViewedAt TEXT,
                            PRIMARY KEY (ArticleId, ArticleIsOfficial)
                        );
                        """);
                }
                var db = new DatabaseService(path);
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString()))
                {
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ViewingHistory') WHERE name='Id'";
                    Check(Convert.ToInt64(command.ExecuteScalar()) == (compositeKey ? 0 : 1), "Fixture does not exercise both real history shapes.");
                }
                Check(db.GetRecentArticles().Count == 0, "Empty history must be readable.");
                var time = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
                db.RecordArticleView(7, false, time);
                var rows = db.GetRecentArticles();
                Check(rows.Count == 1 && rows[0].ArticleId == 7 && !rows[0].ArticleIsOfficial && rows[0].Timestamp.ToUniversalTime() == time, "Registered view did not round-trip.");
                db.RecordArticleView(7, true, time.AddSeconds(1));
                db.RecordArticleView(1_000_000_007, true, time.AddSeconds(2));
                rows = db.GetRecentArticles();
                Check(rows.Count == 3 && rows[0].ArticleId == 1_000_000_007 && rows[1].ArticleIsOfficial, "Sources merged or latest-first order lost.");
                db.RecordArticleView(7, false, time.AddSeconds(3));
                rows = db.GetRecentArticles();
                Check(rows.Count == 3 && rows[0].ArticleId == 7 && !rows[0].ArticleIsOfficial && rows[0].Timestamp.ToUniversalTime() == time.AddSeconds(3), "Repeat view duplicated or did not move to front.");
                Execute(path, compositeKey
                    ? "INSERT INTO ViewingHistory(ArticleId,ArticleIsOfficial,OpenedAt,ViewedAt) VALUES(8,0,'2026-01-01T12:00:04Z',NULL)"
                    : "INSERT INTO ViewingHistory(ArticleId,ArticleIsOfficial,OpenedAt,ViewedAt) VALUES(8,0,'2026-01-01T12:00:04Z','2026-01-01T12:00:04Z')");
                rows = db.GetRecentArticles();
                Check(rows[0].ArticleId == 8 && rows[0].Timestamp.ToUniversalTime() == time.AddSeconds(4), "Nullable ViewedAt must fall back to OpenedAt.");
                for (var i = 0; i < 24; i++) db.RecordArticleView(100 + i, false, time.AddMinutes(1));
                rows = db.GetRecentArticles();
                Check(rows.Count == 20 && rows.Select(r => r.ArticleId).SequenceEqual(Enumerable.Range(100, 20)), "Limit or deterministic tie order lost.");
                Execute(path, "DROP TABLE ViewingHistory");
                var failed = false;
                try { db.GetRecentArticles(); }
                catch (SqliteException) { failed = true; }
                Check(failed, "Read failure was disguised as valid empty history.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, true);
        }
        Console.WriteLine($"Recent history 9.4 OK: {checks} temporary-DB checks, composite/no-Id and new/Id schemas, empty/read/repeat/source/fallback/limit/failure.");
    }
}
