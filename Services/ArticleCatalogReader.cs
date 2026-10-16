using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public enum ArticleCatalogSource { Official, Knowledge, User, Rules }

/// <summary>Read-only, per-operation connections. Only metadata is materialized for the catalog.</summary>
public sealed class ArticleCatalogReader
{
    private readonly string _userPath;
    private readonly string _officialPath;
    private readonly string? _knowledgePath;
    private readonly string _rulesPath;
    public ArticleCatalogReader(string userPath, string officialPath, string? knowledgePath, string? rulesPath = null)
    { _userPath = userPath; _officialPath = officialPath; _knowledgePath = knowledgePath; _rulesPath = rulesPath ?? RpRulesCatalog.ResolvePath(); }

    public List<Article> ReadMetadata(string query = "", CancellationToken cancellationToken = default)
    {
        var result = new List<Article>();
        // Preserve the established source concatenation, with stable per-source tie breakers.
        foreach (var source in new[] { ArticleCatalogSource.Official, ArticleCatalogSource.Knowledge, ArticleCatalogSource.User, ArticleCatalogSource.Rules })
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.AddRange(ReadSourceMetadata(source, query, cancellationToken));
        }
        return RpRulesCatalog.PreferPersonalCopies(result);
    }

    // One lightweight snapshot per source supports existing category/graph/overlay identity lookup.
    // Content remains deferred; the WPF view virtualizes this metadata snapshot.
    public List<Article> ReadSourceMetadata(ArticleCatalogSource source, string query = "", CancellationToken cancellationToken = default) =>
        ReadRows(source, 0, int.MaxValue, query, cancellationToken);

    public List<Article> ReadPage(ArticleCatalogSource source, int offset, int pageSize, string query = "", CancellationToken cancellationToken = default)
    {
        if (offset < 0 || pageSize < 1 || pageSize > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return ReadRows(source, offset, pageSize, query, cancellationToken);
    }

    public List<Article> SearchTitles(ArticleCatalogSource source, string query, int limit, CancellationToken token = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        if (string.IsNullOrWhiteSpace(query)) return [];
        return ReadRows(source, 0, limit, query, token, true);
    }

    public List<Article> ReadByIds(ArticleCatalogSource source, IReadOnlyCollection<int> ids, CancellationToken token = default)
    {
        if (ids.Count > 200 || ids.Any(id => id < 0)) throw new ArgumentOutOfRangeException(nameof(ids));
        return ids.Count == 0 ? [] : ReadRows(source, 0, ids.Count, "", token, false, ids);
    }

    private List<Article> ReadRows(ArticleCatalogSource source, int offset, int pageSize, string query, CancellationToken cancellationToken,
        bool titlesOnly = false, IReadOnlyCollection<int>? ids = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = PathFor(source);
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            if (source is ArticleCatalogSource.Knowledge or ArticleCatalogSource.Rules) return [];
            throw new System.IO.FileNotFoundException("Источник каталога не найден.", path);
        }
        using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(path);
        connection.Open();
        connection.CreateFunction<string?, string?, bool>("catalog_contains", (text, term) => (text ?? "").Contains(term ?? "", StringComparison.OrdinalIgnoreCase), true);
        using var command = connection.CreateCommand();
        command.CommandTimeout = 3;
        var identityFilter = ids is null ? "1=1" : "Id IN (" + string.Join(",", ids.Select((_, i) => "$id" + i)) + ")";
        if (ids is not null)
            foreach (var item in ids.Select((id, index) => (id, index))) command.Parameters.AddWithValue("$id" + item.index, item.id);
        var knowledgeSearch = titlesOnly ? "catalog_contains(e.TitleRu,$query)" :
            "catalog_contains(e.TitleRu,$query) OR catalog_contains(e.SummaryRu,$query) OR catalog_contains(e.ContentRu,$query) OR catalog_contains(e.SemanticType,$query) OR catalog_contains(COALESCE(NULLIF(trim(e.PrimaryCategoryRu),''),e.TypeRu,''),$query) OR catalog_contains(CASE WHEN e.NeedsReview<>0 THEN 'Требует проверки' ELSE '' END,$query)";
        var legacySearch = titlesOnly ? "catalog_contains(Title,$query)" :
            "catalog_contains(Title,$query) OR catalog_contains(Summary,$query) OR catalog_contains(Content,$query) OR catalog_contains(Tags,$query) OR catalog_contains(Category,$query) OR catalog_contains(Flags,$query)";
        command.CommandText = source == ArticleCatalogSource.Knowledge ? $"""
            WITH selected AS (
                SELECT e.Id,e.TitleRu,e.PrimaryCategoryRu,e.TypeRu,e.SummaryRu,e.SemanticType,e.NeedsReview,e.CreatedAt,e.Url
                FROM Entities e
                WHERE ({identityFilter}) AND ($query='' OR ({knowledgeSearch}))
                ORDER BY e.TitleRu COLLATE NOCASE,e.Id LIMIT $limit OFFSET $offset
            )
            SELECT e.Id,e.TitleRu,COALESCE(NULLIF(trim(e.PrimaryCategoryRu),''),e.TypeRu,''),e.SummaryRu,
                   e.SemanticType,CASE WHEN e.NeedsReview<>0 THEN 'Требует проверки' ELSE '' END,0,e.CreatedAt,e.CreatedAt,
                   COALESCE((SELECT group_concat(TitleRu,char(31)) FROM
                       (SELECT c.TitleRu FROM EntityCategories ec JOIN Categories c ON c.Id=ec.CategoryId
                        WHERE ec.EntityId=e.Id ORDER BY c.TitleRu COLLATE NOCASE)),''),
                   COALESCE((SELECT SourceUrl FROM Sources WHERE EntityId=e.Id AND SourceUrl<>'' ORDER BY Id LIMIT 1),e.Url,'')
            FROM selected e ORDER BY e.TitleRu COLLATE NOCASE,e.Id;
            """ : $"""
            SELECT Id,Title,Category,Summary,Tags,Flags,IsFavorite,CreatedAt,UpdatedAt,'',''
            FROM Articles
            WHERE ({identityFilter}) AND ($query='' OR ({legacySearch}))
            ORDER BY {(source == ArticleCatalogSource.Official ? "UpdatedAt DESC,Id" : "Title COLLATE NOCASE,Id")}
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$query", query.Trim());
        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", offset);
        var result = new List<Article>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rawId = reader.GetInt32(0);
            var article = new Article
            {
                Id = source == ArticleCatalogSource.Knowledge ? MvpKnowledgeDatabaseService.ToArticleId(rawId)
                    : source == ArticleCatalogSource.Rules ? RpRulesCatalog.ToArticleId(rawId) : rawId,
                Title = Text(reader,1), Category = Text(reader,2), Summary = Text(reader,3), Tags = Text(reader,4), Flags = Text(reader,5),
                IsFavorite = !reader.IsDBNull(6) && reader.GetInt64(6) != 0,
                CreatedAt = Date(Text(reader,7)), UpdatedAt = Date(Text(reader,8)),
                IsOfficial = source != ArticleCatalogSource.User, IsMvp = source == ArticleCatalogSource.Knowledge,
                IsRpRule = source == ArticleCatalogSource.Rules,
                SourceUrl = Text(reader,10),
                AdditionalCategories = Text(reader,9).Split('\u001f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(category => category != Text(reader,2)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            article.DeferContent(() => ReadContent(source, rawId));
            result.Add(article);
        }
        return result;
    }

    public string ReadContent(ArticleCatalogSource source, int rawId)
    {
        var path = PathFor(source) ?? throw new InvalidOperationException("Не задан путь источника.");
        using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = source == ArticleCatalogSource.Knowledge ? "SELECT ContentRu FROM Entities WHERE Id=$id" : "SELECT Content FROM Articles WHERE Id=$id";
        command.Parameters.AddWithValue("$id", rawId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("Статья больше не существует в источнике.");
        return Text(reader,0);
    }
    private string? PathFor(ArticleCatalogSource source) => source switch
    { ArticleCatalogSource.Official => _officialPath, ArticleCatalogSource.User => _userPath,
      ArticleCatalogSource.Rules => _rulesPath, _ => _knowledgePath };

    public List<ArticleLink> ReadRuleLinks(int uiId)
    {
        var result = new List<ArticleLink>();
        using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(_rulesPath);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,SourceArticleId,TargetArticleId,LinkType,Description FROM Links WHERE SourceArticleId=$id OR TargetArticleId=$id";
        command.Parameters.AddWithValue("$id", checked(-uiId));
        using var r = command.ExecuteReader();
        while (r.Read()) result.Add(new ArticleLink { Id = -r.GetInt32(0), SourceArticleId = RpRulesCatalog.ToArticleId(r.GetInt32(1)),
            TargetArticleId = RpRulesCatalog.ToArticleId(r.GetInt32(2)), SourceIsOfficial = true, TargetIsOfficial = true,
            LinkType = Text(r,3), Description = Text(r,4) });
        r.Close();
        var ids = result.SelectMany(l => new[] { -l.SourceArticleId, -l.TargetArticleId }).Distinct().ToArray();
        if (ids.Length == 0) return result;
        var personal = System.IO.File.Exists(_userPath) ? ReadSourceMetadata(ArticleCatalogSource.User) : [];
        var targets = ReadByIds(ArticleCatalogSource.Rules, ids).ToDictionary(a => a.Id,
            a => personal.FirstOrDefault(p => p.Title == a.Title && p.Category == a.Category && RpRulesCatalog.Equivalent(p,a)) ?? a);
        foreach (var link in result)
        {
            var source = targets[link.SourceArticleId]; var target = targets[link.TargetArticleId];
            link.SourceArticleId = source.Id; link.SourceIsOfficial = source.IsOfficial;
            link.TargetArticleId = target.Id; link.TargetIsOfficial = target.IsOfficial;
        }
        return result;
    }
    private static string Text(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? "" : reader.GetString(index);
    private static DateTime Date(string text) => DateTime.TryParse(text, out var date) ? date : DateTime.MinValue;
}
