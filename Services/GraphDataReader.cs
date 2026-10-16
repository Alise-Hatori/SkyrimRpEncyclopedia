using Microsoft.Data.Sqlite;
using System.IO;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Bounded read-only queries. Caller runs these synchronous SQLite operations on a worker.
/// Cancellation is cooperative: SqliteCommand.Cancel in 10.0.12 is documented as a no-op.</summary>
public sealed class GraphDataReader : IGraphDataReader
{
    private readonly ArticleCatalogReader _articles;
    private readonly Dictionary<ArticleCatalogSource, string?> _paths;
    public GraphDataReader(string user, string official, string? knowledge)
    {
        _articles = new(user, official, knowledge);
        _paths = new() { [ArticleCatalogSource.User] = user, [ArticleCatalogSource.Official] = official, [ArticleCatalogSource.Knowledge] = knowledge };
    }
    public GraphSearchResult Search(string query, IReadOnlyCollection<ArticleCatalogSource> sources, int limit, CancellationToken token)
    {
        if (limit is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(limit));
        var rows = new List<GraphNode>(); var warnings = new List<string>();
        foreach (var source in sources.Distinct().Order())
        {
            token.ThrowIfCancellationRequested();
            try
            {
                RequirePath(source);
                rows.AddRange(_articles.SearchTitles(source, query, limit + 1, token).Select(a => new GraphNode(GraphEntityKey.FromArticle(a), a)));
            }
            catch (Exception e) when (e is SqliteException or IOException or InvalidOperationException) { warnings.Add(source + ": " + e.Message); }
        }
        var ordered = rows.OrderBy(n => n.Article.Title, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Key.SourceKind).ThenBy(n => n.Key.Id).ToArray();
        return new(ordered.Take(limit).ToArray(), ordered.Length > limit, warnings);
    }
    public GraphPage ReadPage(GraphEntityKey root, IReadOnlyDictionary<ArticleCatalogSource, int> cursor, int neighbors, CancellationToken token)
    {
        if (neighbors is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(neighbors));
        RequirePath(root.SourceKind);
        var candidates = new List<GraphEdge>(); var warnings = new List<string>();
        var next = cursor.ToDictionary(x => x.Key, x => x.Value);
        var queryLimit = neighbors * 2;
        var more = false;
        foreach (var store in Enum.GetValues<ArticleCatalogSource>())
        {
            if (store == ArticleCatalogSource.Rules) continue; // Rules are not canonical graph relationships.
            if (store == ArticleCatalogSource.Knowledge && root.SourceKind != store) continue;
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(_paths[store]) || !File.Exists(_paths[store]))
            {
                if (store == root.SourceKind) RequirePath(store);
                else warnings.Add(store + ": источник отсутствует; окружение этого источника не проверено.");
                continue;
            }
            try
            {
                using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(RequirePath(store));
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandTimeout = 3;
                var knowledge = store == ArticleCatalogSource.Knowledge;
                command.CommandText = knowledge ? """
                    SELECT Id,SourceEntityId,1,TargetEntityId,1,RelationType,Evidence FROM Relations
                    WHERE Id>$after AND (SourceEntityId=$id OR TargetEntityId=$id) ORDER BY Id LIMIT $limit
                    """ : """
                    SELECT Id,SourceArticleId,SourceIsOfficial,TargetArticleId,TargetIsOfficial,LinkType,Description FROM Links
                    WHERE Id>$after AND ((SourceArticleId=$id AND SourceIsOfficial=$official) OR
                      (TargetArticleId=$id AND TargetIsOfficial=$official)) ORDER BY Id LIMIT $limit
                    """;
                command.Parameters.AddWithValue("$after", cursor.GetValueOrDefault(store, -1));
                command.Parameters.AddWithValue("$id", !knowledge && root.SourceKind == ArticleCatalogSource.Knowledge ?
                    MvpKnowledgeDatabaseService.ToArticleId(root.Id) : root.Id);
                if (!knowledge) command.Parameters.AddWithValue("$official", root.SourceKind == ArticleCatalogSource.User ? 0 : 1);
                command.Parameters.AddWithValue("$limit", queryLimit + 1);
                using var reader = command.ExecuteReader();
                var count = 0;
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    if (++count > queryLimit) { more = true; break; }
                    GraphEntityKey Key(int id, bool official) => knowledge ? new(ArticleCatalogSource.Knowledge, id) : LegacyKey(id, official);
                    candidates.Add(new(new(store, reader.GetInt32(0)), Key(reader.GetInt32(1), reader.GetInt64(2) != 0),
                        Key(reader.GetInt32(3), reader.GetInt64(4) != 0), Text(reader, 5), Text(reader, 6)));
                }
            }
            catch (SqliteException e) { if (store == root.SourceKind) throw; warnings.Add(store + ": " + e.Message); }
        }
        var keys = new HashSet<GraphEntityKey> { root }; var accepted = new List<GraphEdge>();
        var ordered = candidates.OrderBy(e => e.Key.Store).ThenBy(e => e.Key.Id).ToArray();
        foreach (var edge in ordered)
        {
            var newKeys = new[] { edge.Source, edge.Target }.Distinct().Count(k => !keys.Contains(k));
            if (keys.Count + newKeys > neighbors + 1) { more = true; break; }
            keys.Add(edge.Source); keys.Add(edge.Target); accepted.Add(edge); next[edge.Key.Store] = edge.Key.Id;
        }
        var nodes = new Dictionary<GraphEntityKey, GraphNode>();
        foreach (var group in keys.GroupBy(k => k.SourceKind))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                RequirePath(group.Key);
                foreach (var article in _articles.ReadByIds(group.Key, group.Select(k => k.Id).ToArray(), token))
                { var key = GraphEntityKey.FromArticle(article); nodes[key] = new(key, article); }
            }
            catch (Exception e) when (e is SqliteException or IOException or InvalidOperationException)
            { throw new InvalidOperationException("Не удалось проверить метаданные конца связи: " + group.Key + ". Курсор не должен продвигаться.", e); }
        }
        if (!nodes.ContainsKey(root)) throw new InvalidOperationException("Центральная статья отсутствует или источник недоступен.");
        var valid = accepted.Where(e => nodes.ContainsKey(e.Source) && nodes.ContainsKey(e.Target)).ToArray();
        if (valid.Length != accepted.Count) warnings.Add("Есть связи с отсутствующими концами; они не показаны.");
        return new(root, nodes.Values.ToArray(), valid, next, more, warnings);
    }
    private string RequirePath(ArticleCatalogSource source) => _paths.TryGetValue(source, out var path) && !string.IsNullOrWhiteSpace(path) && File.Exists(path)
        ? path : throw new FileNotFoundException("Источник отсутствует: " + source);
    private static GraphEntityKey LegacyKey(int id, bool official) => !official ? new(ArticleCatalogSource.User, id) :
        id >= MvpKnowledgeDatabaseService.ArticleIdOffset ? new(ArticleCatalogSource.Knowledge, id - MvpKnowledgeDatabaseService.ArticleIdOffset) : new(ArticleCatalogSource.Official, id);
    private static string Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
