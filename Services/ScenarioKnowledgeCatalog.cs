using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SkyrimRpEncyclopedia.Services;

public interface IScenarioKnowledgeCatalog
{
    ScenarioKnowledgeCatalogStatus GetStatus();
    ScenarioKnowledgeSearchResult Search(ScenarioKnowledgeSearchRequest request);
    ScenarioKnowledgeEvidenceResult GetEvidence(int entityId, int maxLength = 1200);
}

/// <summary>
/// Narrow, read-only access to canonical candidates for RP preparation.
/// It never treats an internal wiki relation as narrative proof.
/// </summary>
public sealed class ScenarioKnowledgeCatalog : IScenarioKnowledgeCatalog
{
    public const string ExpectedSchemaVersion = "2.6";
    public const int MaximumEvidenceLength = 4000;
    internal int IntegrityCheckCount { get; private set; }

    private static readonly string[] RequiredTables =
    [
        "BuildInfo", "Entities", "Categories", "EntityCategories", "Sources"
    ];

    private readonly string? _databasePath;

    public ScenarioKnowledgeCatalog(string? databasePath = null)
    {
        _databasePath = MvpKnowledgeDatabaseService.ResolveDatabasePath(databasePath);
    }

    public ScenarioKnowledgeCatalogStatus GetStatus() => Inspect();
    public ScenarioKnowledgeCatalogStatus CheckIntegrity() => Inspect(checkIntegrity: true);

    public ScenarioKnowledgeSearchResult Search(ScenarioKnowledgeSearchRequest request)
    {
        var status = Inspect();
        if (!status.IsCompatible)
            return new ScenarioKnowledgeSearchResult { Status = status };

        try
        {
            using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(_databasePath!);
            connection.Open();
            connection.CreateFunction<string?, string?, bool>("kb_contains", (value, term) =>
                (value ?? "").Contains(term ?? "", StringComparison.OrdinalIgnoreCase), isDeterministic: true);
            connection.CreateFunction<string?, string?>("kb_excerpt", value => LimitedText(value, 322), isDeterministic: true);
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT e.Id, e.TitleRu, e.SemanticType, e.PrimaryCategoryRu,
                       COALESCE(kb_excerpt(e.SummaryRu), kb_excerpt(e.ContentRu), '') AS ContextExcerpt,
                       COALESCE(e.Confidence, 0), COALESCE(e.NeedsReview, 0),
                       COALESCE((
                           SELECT s.SourceUrl
                           FROM Sources s
                           WHERE s.EntityId = e.Id AND s.SourceUrl IS NOT NULL AND s.SourceUrl <> ''
                           ORDER BY s.Id LIMIT 1
                       ), e.Url, '') AS SourceUrl,
                       COALESCE((
                           SELECT group_concat(TitleRu, char(31))
                           FROM (
                               SELECT c.TitleRu
                               FROM EntityCategories ec
                               INNER JOIN Categories c ON c.Id = ec.CategoryId
                               WHERE ec.EntityId = e.Id
                               ORDER BY c.TitleRu COLLATE NOCASE
                           )
                       ), '') AS CategoryTitles
                FROM Entities e
                WHERE ($semanticType = '' OR e.SemanticType = $semanticType)
                  AND ($title = '' OR kb_contains(e.TitleRu, $title))
                  AND ($category = '' OR e.PrimaryCategoryRu = $category OR EXISTS (
                      SELECT 1
                      FROM EntityCategories ec
                      INNER JOIN Categories c ON c.Id = ec.CategoryId
                      WHERE ec.EntityId = e.Id AND c.TitleRu = $category
                  ))
                  AND ($includeNeedsReview = 1 OR COALESCE(e.NeedsReview, 0) = 0)
                ORDER BY e.TitleRu COLLATE NOCASE, e.Id
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$semanticType", request.SemanticType.Trim());
            command.Parameters.AddWithValue("$category", request.Category.Trim());
            command.Parameters.AddWithValue("$title", request.Title.Trim());
            command.Parameters.AddWithValue("$includeNeedsReview", request.IncludeNeedsReview ? 1 : 0);
            command.Parameters.AddWithValue("$limit", Math.Clamp(request.Limit, 1, 50));

            var candidates = new List<ScenarioKnowledgeCandidate>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sourceUrl = Text(reader, 7);
                candidates.Add(new ScenarioKnowledgeCandidate
                {
                    EntityId = reader.GetInt32(0),
                    Title = Text(reader, 1),
                    SemanticType = Text(reader, 2),
                    PrimaryCategory = Text(reader, 3),
                    ContextExcerpt = Truncate(Text(reader, 4), 320),
                    Confidence = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    NeedsReview = !reader.IsDBNull(6) && reader.GetInt32(6) != 0,
                    SourceUrl = MvpKnowledgeDatabaseService.TryGetSafeSourceUri(sourceUrl, out _) ? sourceUrl : "",
                    Categories = ParseCategories(Text(reader, 8))
                });
            }
            return new ScenarioKnowledgeSearchResult { Status = status, Candidates = candidates };
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ScenarioKnowledgeSearchResult
            {
                Status = new ScenarioKnowledgeCatalogStatus
                {
                    IsAvailable = true,
                    IsCompatible = false,
                    Diagnostic = "Не удалось прочитать Knowledge DB: " + exception.Message,
                    SchemaVersion = status.SchemaVersion
                }
            };
        }
    }

    public ScenarioKnowledgeEvidenceResult GetEvidence(int entityId, int maxLength = 1200)
    {
        var status = Inspect();
        ScenarioKnowledgeEvidenceResult Reject(string reason, string diagnostic) => new() { Status = status, Reason = reason, Diagnostic = diagnostic };
        if (!status.IsCompatible) return Reject("incompatible", status.Diagnostic);
        if (entityId <= 0 || entityId >= MvpKnowledgeDatabaseService.ArticleIdOffset)
            return Reject("invalid_id", "Нужен исходный EntityId, а не ID статьи WPF.");
        if (maxLength < 1 || maxLength > MaximumEvidenceLength)
            return Reject("invalid_limit", $"Длина должна быть от 1 до {MaximumEvidenceLength} символов UTF-16.");
        try
        {
            using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(_databasePath!);
            connection.Open();
            // Two UTF-16 units of lookahead preserve truncation detection around surrogate pairs.
            connection.CreateFunction<string?, string?>("kb_fragment", value => LimitedText(value, maxLength + 2), isDeterministic: true);
            var sourceColumns = GetColumns(connection, "Sources");
            string MetadataColumn(string column) => sourceColumns.Contains(column) ? $"COALESCE(s.{column}, '')" : "''";
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT e.TitleRu, e.SemanticType, e.NeedsReview, e.Confidence,
                       COALESCE(kb_fragment(e.SummaryRu), kb_fragment(e.ContentRu), ''),
                       CASE WHEN kb_fragment(e.SummaryRu) IS NOT NULL THEN 'SummaryRu' ELSE 'ContentRu' END,
                       COALESCE(NULLIF(trim(s.SourceUrl), ''), e.Url, ''), s.Id,
                       {MetadataColumn("SourceName")}, {MetadataColumn("RevisionTimestamp")},
                       {MetadataColumn("RetrievedAt")}, {MetadataColumn("License")}
                FROM Entities e
                LEFT JOIN Sources s ON s.Id = (
                    SELECT Id FROM Sources WHERE EntityId = e.Id AND NULLIF(trim(SourceUrl), '') IS NOT NULL ORDER BY Id LIMIT 1
                )
                WHERE e.Id = $id LIMIT 1;
                """;
            command.Parameters.AddWithValue("$id", entityId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return Reject("not_found", "Сущность не найдена.");
            if (reader.GetValue(2) is not long review || (review != 0 && review != 1))
                return Reject("invalid_quality", "NeedsReview отсутствует или некорректен.");
            if (review == 1) return Reject("needs_review", "Сущность требует проверки и не допускается в контекст.");
            if (reader.GetValue(3) is not double && reader.GetValue(3) is not long)
                return Reject("invalid_quality", "Confidence отсутствует или некорректен.");
            var confidence = reader.GetDouble(3);
            if (!double.IsFinite(confidence) || confidence < 0 || confidence > 1)
                return Reject("invalid_quality", "Confidence вне диапазона 0..1.");
            var title = Text(reader, 0);
            var url = Text(reader, 6);
            if (string.IsNullOrWhiteSpace(title)) return Reject("invalid_title", "Заголовок отсутствует.");
            if (!MvpKnowledgeDatabaseService.TryGetSafeSourceUri(url, out _))
                return Reject("invalid_source_url", "Нет допустимого URL источника HTTP(S).");
            var text = Text(reader, 4);
            if (string.IsNullOrWhiteSpace(text)) return Reject("empty_text", "Нет непустого SummaryRu или ContentRu.");
            var fragment = SafePrefix(text, maxLength);
            if (fragment.Length == 0) return Reject("invalid_limit", "Лимит недостаточен для первого Unicode-символа.");
            var sourceId = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7);
            var sourceName = Text(reader, 8);
            var revision = Text(reader, 9);
            var retrieved = Text(reader, 10);
            var license = Text(reader, 11);
            return new ScenarioKnowledgeEvidenceResult
            {
                Status = status,
                Evidence = new ScenarioKnowledgeEvidence
                {
                    EntityId = entityId, Title = title, SemanticType = Text(reader, 1), SourceUrl = url,
                    SourceId = sourceId, SourceName = sourceName, RevisionTimestamp = revision, RetrievedAt = retrieved, License = license,
                    Confidence = confidence, NeedsReview = false, TextField = Text(reader, 5), Fragment = fragment,
                    IsTruncated = text.Length > fragment.Length, FragmentSha256 = Hash(fragment),
                    SourceMetadataSha256 = Hash(JsonSerializer.Serialize(new object?[] { entityId, sourceId, url, sourceName, revision, retrieved, license }))
                }
            };
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidCastException or FormatException or OverflowException)
        {
            return Reject("read_error", "Не удалось прочитать свидетельство: " + exception.Message);
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string? LimitedText(string? value, int limit) => string.IsNullOrWhiteSpace(value) ? null : SafePrefix(value.Trim(), limit);
    private static string SafePrefix(string value, int limit)
    {
        var length = Math.Min(value.Length, limit);
        if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length])) length--;
        return value[..length];
    }

    private ScenarioKnowledgeCatalogStatus Inspect(bool checkIntegrity = false)
    {
        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            return new ScenarioKnowledgeCatalogStatus
            {
                Diagnostic = "Knowledge DB не задана или файл отсутствует. Автономные сценарии доступны."
            };
        }

        try
        {
            using var connection = MvpKnowledgeDatabaseService.OpenReadOnlyConnection(_databasePath!);
            connection.Open();
            if (checkIntegrity)
            {
                using var integrityCommand = connection.CreateCommand();
                integrityCommand.CommandText = "PRAGMA integrity_check;";
                IntegrityCheckCount++;
                if (!string.Equals(integrityCommand.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                    return new ScenarioKnowledgeCatalogStatus { IsAvailable = true, Diagnostic = "Knowledge DB не прошла integrity_check." };
            }

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tablesCommand = connection.CreateCommand())
            {
                tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using var reader = tablesCommand.ExecuteReader();
                while (reader.Read()) tables.Add(Text(reader, 0));
            }
            var missingTables = RequiredTables.Where(table => !tables.Contains(table)).ToArray();
            if (missingTables.Length > 0)
            {
                return new ScenarioKnowledgeCatalogStatus
                {
                    IsAvailable = true,
                    Diagnostic = "Knowledge DB не содержит обязательные таблицы: " + string.Join(", ", missingTables) + "."
                };
            }

            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "SELECT Value FROM BuildInfo WHERE Key = 'SchemaVersion' LIMIT 1;";
            var schemaVersion = versionCommand.ExecuteScalar()?.ToString() ?? "";
            if (!string.Equals(schemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal))
            {
                return new ScenarioKnowledgeCatalogStatus
                {
                    IsAvailable = true,
                    Diagnostic = $"Knowledge DB имеет SchemaVersion '{schemaVersion}', ожидается '{ExpectedSchemaVersion}'.",
                    SchemaVersion = schemaVersion
                };
            }

            foreach (var requirement in RequiredColumns)
            {
                if (!GetColumns(connection, requirement.Table).IsSupersetOf(requirement.Columns))
                {
                    return new ScenarioKnowledgeCatalogStatus
                    {
                        IsAvailable = true,
                        Diagnostic = $"Knowledge DB не содержит ожидаемые столбцы таблицы {requirement.Table}.",
                        SchemaVersion = schemaVersion
                    };
                }
            }

            return new ScenarioKnowledgeCatalogStatus
            {
                IsAvailable = true,
                IsCompatible = true,
                SchemaVersion = schemaVersion,
                Diagnostic = "Knowledge DB доступна только для чтения."
            };
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ScenarioKnowledgeCatalogStatus
            {
                IsAvailable = true,
                Diagnostic = "Не удалось проверить Knowledge DB: " + exception.Message
            };
        }
    }

    private static readonly (string Table, HashSet<string> Columns)[] RequiredColumns =
    [
        ("BuildInfo", new(StringComparer.OrdinalIgnoreCase) { "Key", "Value" }),
        ("Entities", new(StringComparer.OrdinalIgnoreCase) { "Id", "TitleRu", "Url", "SemanticType", "PrimaryCategoryRu", "SummaryRu", "ContentRu", "Confidence", "NeedsReview" }),
        ("Categories", new(StringComparer.OrdinalIgnoreCase) { "Id", "TitleRu" }),
        ("EntityCategories", new(StringComparer.OrdinalIgnoreCase) { "EntityId", "CategoryId" }),
        ("Sources", new(StringComparer.OrdinalIgnoreCase) { "Id", "EntityId", "SourceUrl" })
    ];

    private static HashSet<string> GetColumns(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) columns.Add(Text(reader, 1));
        return columns;
    }

    private static string Text(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    private static IReadOnlyList<string> ParseCategories(string value) =>
        value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : SafePrefix(value, length) + "…";
}
