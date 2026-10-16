using System.IO;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Explicit Knowledge-only map, keyset batches in one read-only snapshot.
/// Cancellation is cooperative between rows/batches; SqliteCommand.Cancel is not an interrupt.</summary>
public sealed class GraphMapReader(string? path) : IGraphMapReader
{
    public GraphMapSnapshot Read(CancellationToken token, IProgress<GraphMapProgress>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Для полной карты укажите существующую Knowledge DB в SKYRIM_RP_MVP_DB.", path);
        using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(path);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandTimeout = 3;
        command.CommandText = "SELECT (SELECT COUNT(*) FROM Entities),(SELECT COUNT(*) FROM Relations)";
        long nodeCount, edgeCount;
        using (var counts = command.ExecuteReader()) { counts.Read(); nodeCount = counts.GetInt64(0); edgeCount = counts.GetInt64(1); }
        var nodes = new Dictionary<GraphEntityKey, GraphMapNode>(); var edges = new List<GraphEdge>();
        long after = long.MinValue;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            command.Parameters.Clear(); command.Parameters.AddWithValue("$after", after); command.Parameters.AddWithValue("$batch", 500);
            command.CommandText = """
                SELECT e.Id,e.TitleRu,COALESCE(NULLIF(trim(e.PrimaryCategoryRu),''),e.TypeRu,''),
                    COALESCE((SELECT group_concat(c.TitleRu,char(31)) FROM EntityCategories ec JOIN Categories c ON c.Id=ec.CategoryId WHERE ec.EntityId=e.Id),'')
                FROM Entities e WHERE e.Id>$after ORDER BY e.Id LIMIT $batch
                """;
            var loaded = 0;
            using (var rows = command.ExecuteReader()) while (rows.Read())
            {
                token.ThrowIfCancellationRequested(); loaded++; after = rows.GetInt64(0);
                var key = new GraphEntityKey(ArticleCatalogSource.Knowledge, rows.GetInt32(0));
                // Reuse the established classifier, without retaining Article or its text in the snapshot.
                var category = rows.IsDBNull(2) ? "" : rows.GetString(2); var title = rows.IsDBNull(1) ? "" : rows.GetString(1);
                nodes.Add(key, new(key, title, category, ArticleGraphTypeClassifier.Classify(new Article
                    { Title = title, Category = category, AdditionalCategories = rows.GetString(3).Split('\u001f',StringSplitOptions.RemoveEmptyEntries) })));
            }
            progress?.Report(new(nodes.Count, edges.Count, nodeCount, edgeCount, "Сущности"));
            if (loaded < 500) break;
        }
        after = long.MinValue;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            command.Parameters.Clear(); command.Parameters.AddWithValue("$after", after); command.Parameters.AddWithValue("$batch", 2000);
            command.CommandText = "SELECT Id,SourceEntityId,TargetEntityId,RelationType FROM Relations WHERE Id>$after ORDER BY Id LIMIT $batch";
            var loaded = 0;
            using (var rows = command.ExecuteReader()) while (rows.Read())
            {
                token.ThrowIfCancellationRequested(); loaded++; after = rows.GetInt64(0);
                var source = new GraphEntityKey(ArticleCatalogSource.Knowledge, rows.GetInt32(1));
                var target = new GraphEntityKey(ArticleCatalogSource.Knowledge, rows.GetInt32(2));
                if (!nodes.ContainsKey(source) || !nodes.ContainsKey(target)) throw new InvalidDataException($"Связь #{after}: отсутствует конец. Полная карта не публикуется.");
                edges.Add(new(new(ArticleCatalogSource.Knowledge, rows.GetInt32(0)), source, target, rows.IsDBNull(3) ? "" : rows.GetString(3), ""));
            }
            progress?.Report(new(nodes.Count, edges.Count, nodeCount, edgeCount, "Связи"));
            if (loaded < 2000) break;
        }
        token.ThrowIfCancellationRequested();
        if (nodes.Count != nodeCount || edges.Count != edgeCount) throw new InvalidDataException("COUNT не совпадает с полным снимком.");
        return new(nodes, edges, nodeCount, edgeCount);
    }
    public Article ReadArticle(GraphEntityKey key, CancellationToken token)
    {
        if (key.SourceKind != ArticleCatalogSource.Knowledge) throw new ArgumentException("Полная карта читает только Knowledge DB.");
        return new ArticleCatalogReader("", "", path).ReadByIds(key.SourceKind, new[] { key.Id }, token).Single();
    }
}
