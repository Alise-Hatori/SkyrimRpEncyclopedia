using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using System.IO;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Read-only projection of a Skyrim Knowledge Base V2 database for the WPF UI.
/// No WPF, official or user tables are modified by this service.
/// </summary>
public sealed class MvpKnowledgeDatabaseService
{
    public const string DatabasePathEnvironmentVariable = "SKYRIM_RP_MVP_DB";
    // The existing UI routes legacy sources through a bool (IsOfficial).  MVP
    // uses the canonical visual path but a disjoint ID space and IsMvp=true,
    // so an MVP Id can never collide with official.db or user.db identifiers.
    public const int ArticleIdOffset = 1_000_000_000;
    public const int LinkIdOffset = 1_000_000_000;

    private readonly string? _databasePath;

    public MvpKnowledgeDatabaseService(string? databasePath = null)
    {
        _databasePath = ResolveDatabasePath(databasePath);
    }

    public string? DatabasePath => _databasePath;

    public static string? ResolveDatabasePath(string? databasePath = null, string? applicationDirectory = null)
    {
        var configured = string.IsNullOrWhiteSpace(databasePath)
            ? Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable)
            : databasePath;
        // Explicit paths must not silently fall back to a different corpus on failure.
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        var local = Path.Combine(applicationDirectory ?? AppContext.BaseDirectory, "Data", "knowledge.db");
        return File.Exists(local) ? Path.GetFullPath(local) : null;
    }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_databasePath) &&
        File.Exists(_databasePath);

    private SqliteConnection CreateConnection()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "MVP-база не задана. Укажите существующий путь в SKYRIM_RP_MVP_DB.");
        }

        return OpenReadOnlyConnection(_databasePath!);
    }

    internal static SqliteConnection OpenReadOnlyConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };
        return new SqliteConnection(builder.ToString());
    }

    public List<Article> GetArticles()
    {
        if (!IsAvailable)
            return new List<Article>();

        var articles = new List<Article>();
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.Id, e.TitleRu, e.PrimaryCategoryRu, e.SummaryRu,
                   e.ContentRu, e.SemanticType, e.TypeRu, e.NeedsReview,
                   e.CreatedAt,
                   COALESCE(
                       (SELECT group_concat(CategoryTitle, char(31))
                        FROM (
                            SELECT c.TitleRu AS CategoryTitle
                            FROM EntityCategories ec
                            INNER JOIN Categories c ON c.Id = ec.CategoryId
                            WHERE ec.EntityId = e.Id
                            ORDER BY c.TitleRu COLLATE NOCASE
                        )),
                       '') AS CategoryTitles,
                   COALESCE(
                       (SELECT s.SourceUrl
                        FROM Sources s
                        WHERE s.EntityId = e.Id
                          AND s.SourceUrl IS NOT NULL
                          AND s.SourceUrl <> ''
                        ORDER BY s.Id
                        LIMIT 1),
                       e.Url,
                       '') AS SourceUrl
            FROM Entities e
            ORDER BY e.TitleRu COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int entityId = reader.GetInt32(0);
            string primaryCategory = Text(reader, 2);
            articles.Add(new Article
            {
                Id = ToArticleId(entityId),
                Title = Text(reader, 1),
                Category = NonEmpty(primaryCategory, Text(reader, 6)),
                Summary = Text(reader, 3),
                Content = Text(reader, 4),
                Tags = Text(reader, 5),
                AdditionalCategories = ParseAdditionalCategories(Text(reader, 9), primaryCategory),
                Flags = !reader.IsDBNull(7) && reader.GetInt32(7) != 0
                    ? "Требует проверки"
                    : "",
                IsOfficial = true,
                IsMvp = true,
                SourceUrl = Text(reader, 10),
                CreatedAt = ParseDate(Text(reader, 8)),
                UpdatedAt = ParseDate(Text(reader, 8))
            });
        }
        return articles;
    }

    public List<ArticleLink> GetLinksForArticle(int articleId)
    {
        if (!IsAvailable || !TryToEntityId(articleId, out int entityId))
            return new List<ArticleLink>();

        var links = new List<ArticleLink>();
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, SourceEntityId, TargetEntityId, RelationType, Evidence
            FROM Relations
            WHERE SourceEntityId = $entityId OR TargetEntityId = $entityId
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("$entityId", entityId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            links.Add(new ArticleLink
            {
                Id = ToLinkId(reader.GetInt32(0)),
                SourceArticleId = ToArticleId(reader.GetInt32(1)),
                SourceIsOfficial = true,
                TargetArticleId = ToArticleId(reader.GetInt32(2)),
                TargetIsOfficial = true,
                LinkType = Text(reader, 3),
                Description = Text(reader, 4)
            });
        }
        return links;
    }

    public static bool TryGetSafeSourceUri(string? value, out Uri? uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) &&
            (candidate.Scheme == Uri.UriSchemeHttps || candidate.Scheme == Uri.UriSchemeHttp))
        {
            uri = candidate;
            return true;
        }

        uri = null;
        return false;
    }

    public static int ToArticleId(int entityId)
    {
        if (entityId < 0 || entityId > int.MaxValue - ArticleIdOffset)
            throw new InvalidOperationException("Идентификатор сущности MVP вне безопасного диапазона.");
        return ArticleIdOffset + entityId;
    }

    public static int ToLinkId(int relationId)
    {
        if (relationId < 0 || relationId > int.MaxValue - LinkIdOffset)
            throw new InvalidOperationException("Идентификатор связи MVP вне безопасного диапазона.");
        return LinkIdOffset + relationId;
    }

    private static bool TryToEntityId(int articleId, out int entityId)
    {
        entityId = articleId - ArticleIdOffset;
        return articleId >= ArticleIdOffset && entityId >= 0;
    }

    private static string Text(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    private static string NonEmpty(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static IReadOnlyCollection<string> ParseAdditionalCategories(
        string value,
        string primaryCategory) =>
        value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(category => !category.Equals(primaryCategory, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static DateTime ParseDate(string value) =>
        DateTime.TryParse(value, out var date) ? date : DateTime.UnixEpoch;
}
