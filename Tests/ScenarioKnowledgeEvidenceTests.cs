using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Models;
using System.Security.Cryptography;
using System.Text;

static class ScenarioKnowledgeEvidenceTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "skyrim-rp-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "knowledge.db");
        try
        {
            using (var connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE BuildInfo(Key TEXT PRIMARY KEY, Value TEXT);
                    INSERT INTO BuildInfo VALUES('SchemaVersion','2.6');
                    CREATE TABLE Entities(Id INTEGER PRIMARY KEY, TitleRu TEXT, Url TEXT, SemanticType TEXT,
                        PrimaryCategoryRu TEXT, SummaryRu TEXT, ContentRu TEXT, Confidence REAL, NeedsReview INTEGER);
                    CREATE TABLE Sources(Id INTEGER PRIMARY KEY, EntityId INTEGER, SourceUrl TEXT,
                        SourceName TEXT, RevisionTimestamp TEXT, RetrievedAt TEXT, License TEXT);
                    CREATE TABLE Categories(Id INTEGER PRIMARY KEY, TitleRu TEXT);
                    CREATE TABLE EntityCategories(EntityId INTEGER, CategoryId INTEGER);
                    INSERT INTO Entities VALUES(1,'Лидия','https://example.test/entity','NPC','Персонажи','  Краткое описание  ','Полное содержимое',0.9,0);
                    INSERT INTO Entities VALUES(2,'Пустое summary','https://example.test/two','Item','Предметы','', 'Текст два',0.8,0);
                    INSERT INTO Entities VALUES(3,'Null summary','https://example.test/three','Place','Локации',NULL,'Текст три',0.7,0);
                    INSERT INTO Entities VALUES(4,'Пробелы','https://example.test/four','Place','Локации',char(9)||char(10)||char(160),'Текст четыре',0.7,0);
                    INSERT INTO Entities VALUES(5,'Нет текста','https://example.test/five','Place','Локации',' ',NULL,0.7,0);
                    INSERT INTO Entities VALUES(6,'Нет URL',NULL,'NPC','Персонажи','Есть текст','',0.8,0);
                    INSERT INTO Entities VALUES(7,'Плохой URL','file:///bad','NPC','Персонажи','Есть текст','',0.8,0);
                    INSERT INTO Entities VALUES(8,'Проверка','https://example.test/eight','NPC','Персонажи','Есть текст','',0.8,1);
                    INSERT INTO Entities VALUES(9,'Качество','https://example.test/nine','NPC','Персонажи','Есть текст','',2.0,0);
                    INSERT INTO Entities VALUES(10,'','https://example.test/ten','NPC','Персонажи','Есть текст','',0.8,0);
                    INSERT INTO Entities VALUES(11,'Unicode','https://example.test/eleven','Item','Предметы','А😀БВГ','',0.8,0);
                    INSERT INTO Entities VALUES(12,'Нет качества','https://example.test/twelve','NPC','Персонажи','Текст','',0.8,NULL);
                    INSERT INTO Entities VALUES(13,'Нечисловое качество','https://example.test/thirteen','NPC','Персонажи','Текст','','bad',0);
                    INSERT INTO Sources VALUES(1,1,'https://example.test/source','Russian Skyrim Wiki','2026-10-12','2026-10-13','CC BY-SA');
                    INSERT INTO Categories VALUES(1,'Персонажи');
                    INSERT INTO EntityCategories VALUES(1,1);
                    """;
                command.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();
            var before = SHA256.HashData(File.ReadAllBytes(path));
            var catalog = new ScenarioKnowledgeCatalog(path);
            Require(catalog.GetStatus().IsCompatible, "Compatible fixture rejected.");
            var evidence = catalog.GetEvidence(1).Evidence!;
            Require(evidence is not null && evidence.EntityId == 1 && evidence.Title == "Лидия" &&
                evidence.SourceUrl == "https://example.test/source" && evidence.SourceId == 1 &&
                evidence.RevisionTimestamp == "2026-10-12" && evidence.License == "CC BY-SA" &&
                evidence.TextField == "SummaryRu" && evidence.Fragment == "Краткое описание" && !evidence.IsTruncated,
                "Evidence or source metadata lost.");
            Require(evidence!.FragmentSha256 == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.Fragment))).ToLowerInvariant() &&
                evidence.FragmentSha256 == catalog.GetEvidence(1).Evidence!.FragmentSha256 &&
                evidence.SourceMetadataSha256 == catalog.GetEvidence(1, 3).Evidence!.SourceMetadataSha256,
                "Evidence fingerprints are unstable or use the wrong text.");
            Require(catalog.GetEvidence(1, 3).Evidence is { Fragment: "Кра", IsTruncated: true }, "Length limit failed.");
            Require(catalog.GetEvidence(11, 2).Evidence is { Fragment: "А", IsTruncated: true }, "Truncation split a surrogate pair.");
            Require(catalog.GetEvidence(11, 1).Evidence is { Fragment: "А", IsTruncated: true }, "Unicode lookahead lost truncation status.");
            foreach (var id in new[] { 2, 3, 4 })
                Require(catalog.GetEvidence(id).Evidence is { TextField: "ContentRu" }, "Empty summary did not fall back to content.");
            foreach (var (id, reason) in new[] { (5, "empty_text"), (6, "invalid_source_url"), (7, "invalid_source_url"),
                (8, "needs_review"), (9, "invalid_quality"), (10, "invalid_title"), (12, "invalid_quality"), (13, "invalid_quality"), (99, "not_found"), (-1, "invalid_id"), (1000000001, "invalid_id") })
                Require(catalog.GetEvidence(id).Evidence is null && catalog.GetEvidence(id).Reason == reason, "Invalid evidence was accepted: " + id);
            Require(catalog.GetEvidence(1, 0).Reason == "invalid_limit" && catalog.GetEvidence(1, 4001).Reason == "invalid_limit", "Unsafe limit accepted.");
            foreach (var title in new[] { "ЛИДИЯ", "лидия", "ЛиДиЯ", "ИДИ" })
                Require(catalog.Search(new() { Title = title }).Candidates.Single().EntityId == 1, "Cyrillic case-insensitive substring search failed.");
            Require(catalog.Search(new() { Title = "Пустое summary" }).Candidates.Single().ContextExcerpt == "Текст два", "Search summary fallback failed.");
            Require(catalog.Search(new() { SemanticType = "NPC", Category = "Персонажи" }).Candidates.All(c => !c.NeedsReview), "Existing filters changed.");
            Require(new ScenarioCanonicalAnchorSelector(catalog).SuggestCandidates("investigation", 42).Anchors.All(a => !a.NeedsReview && a.Status == ScenarioCanonicalAnchorStatus.Proposed), "Selector contract changed.");
            Require(catalog.IntegrityCheckCount == 0, "Hot-path search or evidence ran a full integrity check.");
            Require(catalog.CheckIntegrity().IsCompatible && catalog.IntegrityCheckCount == 1, "Explicit integrity diagnostic failed.");
            using (var readOnly = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(path))
            {
                readOnly.Open();
                using var writeAttempt = readOnly.CreateCommand();
                writeAttempt.CommandText = "DELETE FROM Entities WHERE Id=1";
                try { writeAttempt.ExecuteNonQuery(); throw new InvalidOperationException("Read-only connection allowed a write."); }
                catch (SqliteException e) when (e.SqliteErrorCode == 8) { }
            }
            SqliteConnection.ClearAllPools();
            Require(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Read operations changed fixture bytes.");
            using (var mutation = new SqliteConnection("Data Source=" + path))
            {
                mutation.Open();
                using var command = mutation.CreateCommand();
                command.CommandText = "UPDATE BuildInfo SET Value='2.5' WHERE Key='SchemaVersion'";
                command.ExecuteNonQuery();
            }
            Require(!catalog.GetStatus().IsCompatible && catalog.Search(new()).Candidates.Count == 0 && catalog.GetEvidence(1).Evidence is null,
                "Compatibility was cached indefinitely after a fixture change.");
            Console.WriteLine("Knowledge evidence OK: IDs, metadata, fallback, UTF-16 bounds, SHA-256, Cyrillic, read-only and explicit integrity.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
