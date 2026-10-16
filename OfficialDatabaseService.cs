using System.IO;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public class OfficialDatabaseService
{
    public const string DatabasePathEnvironmentVariable = "SKYRIM_RP_OFFICIAL_DB";

    private readonly string _databasePath;

    public OfficialDatabaseService(string? databasePath = null)
    {
        _databasePath = ResolveDatabasePath(databasePath);
        UserDatabasePreflight.ValidateExisting(_databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        InitializeDatabase();
        SeedDefaultArticles();
    }

    public string DatabasePath => _databasePath;

    public static string ResolveDatabasePath(string? databasePath = null)
    {
        string? configuredPath = string.IsNullOrWhiteSpace(databasePath)
            ? Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable)
            : databasePath;

        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyrimRpEncyclopedia",
            "official.db");
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(
            $"Data Source={_databasePath}");
    }

    private void InitializeDatabase()
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Articles
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Category TEXT NOT NULL,
                Summary TEXT,
                Content TEXT,
                Tags TEXT,
                Flags TEXT,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Links
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                SourceArticleId INTEGER NOT NULL,

                SourceIsOfficial INTEGER NOT NULL DEFAULT 1,

                TargetArticleId INTEGER NOT NULL,

                TargetIsOfficial INTEGER NOT NULL DEFAULT 0,

                LinkType TEXT NOT NULL DEFAULT '',

                Description TEXT
            );
            """;

        command.ExecuteNonQuery();
    }

    public List<Article> GetArticles()
    {
        var articles = new List<Article>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                Title,
                Category,
                Summary,
                Content,
                Tags,
                Flags,
                IsFavorite,
                CreatedAt,
                UpdatedAt
            FROM Articles
            ORDER BY UpdatedAt DESC;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            articles.Add(new Article
            {
                Id = reader.GetInt32(0),

                Title = reader.GetString(1),

                Category = reader.GetString(2),

                Summary = reader.IsDBNull(3)
                    ? ""
                    : reader.GetString(3),

                Content = reader.IsDBNull(4)
                    ? ""
                    : reader.GetString(4),

                Tags = reader.IsDBNull(5)
                    ? ""
                    : reader.GetString(5),

                Flags = reader.IsDBNull(6)
                    ? ""
                    : reader.GetString(6),

                IsFavorite =
                    reader.GetInt32(7) == 1,

                IsOfficial = true,

                CreatedAt =
                    DateTime.Parse(
                        reader.GetString(8)),

                UpdatedAt =
                    DateTime.Parse(
                        reader.GetString(9))
            });
        }

        return articles;
    }

    public void SetFavorite(
        int id,
        bool isFavorite)
    {
        using var connection =
            CreateConnection();

        connection.Open();

        using var command =
            connection.CreateCommand();

        command.CommandText = """
            UPDATE Articles
            SET
                IsFavorite = $favorite
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id);

        command.Parameters.AddWithValue(
            "$favorite",
            isFavorite ? 1 : 0);

        command.ExecuteNonQuery();
    }

    public void AddLink(ArticleLink link)
    {
        using var connection =
            CreateConnection();

        connection.Open();

        using var command =
            connection.CreateCommand();

        command.CommandText = """
            INSERT INTO Links
            (
                SourceArticleId,
                SourceIsOfficial,
                TargetArticleId,
                TargetIsOfficial,
                LinkType,
                Description
            )
            VALUES
            (
                $sourceId,
                $sourceOfficial,
                $targetId,
                $targetOfficial,
                $type,
                $description
            );
            """;

        command.Parameters.AddWithValue(
            "$sourceId",
            link.SourceArticleId);

        command.Parameters.AddWithValue(
            "$sourceOfficial",
            link.SourceIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$targetId",
            link.TargetArticleId);

        command.Parameters.AddWithValue(
            "$targetOfficial",
            link.TargetIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$type",
            link.LinkType);

        command.Parameters.AddWithValue(
            "$description",
            link.Description);

        command.ExecuteNonQuery();
    }

    public List<ArticleLink> GetLinksForArticle(
        int articleId,
        bool isOfficial)
    {
        var links =
            new List<ArticleLink>();

        using var connection =
            CreateConnection();

        connection.Open();

        using var command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                SourceArticleId,
                SourceIsOfficial,
                TargetArticleId,
                TargetIsOfficial,
                LinkType,
                Description
            FROM Links
            WHERE
                (
                    SourceArticleId = $articleId
                    AND SourceIsOfficial = $isOfficial
                )
                OR
                (
                    TargetArticleId = $articleId
                    AND TargetIsOfficial = $isOfficial
                )
            ORDER BY Id;
            """;

        command.Parameters.AddWithValue(
            "$articleId",
            articleId);

        command.Parameters.AddWithValue(
            "$isOfficial",
            isOfficial ? 1 : 0);

        using var reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            links.Add(new ArticleLink
            {
                Id = reader.GetInt32(0),

                SourceArticleId =
                    reader.GetInt32(1),

                SourceIsOfficial =
                    reader.GetInt32(2) == 1,

                TargetArticleId =
                    reader.GetInt32(3),

                TargetIsOfficial =
                    reader.GetInt32(4) == 1,

                LinkType =
                    reader.IsDBNull(5)
                        ? ""
                        : reader.GetString(5),

                Description =
                    reader.IsDBNull(6)
                        ? ""
                        : reader.GetString(6)
            });
        }

        return links;
    }

    private void SeedDefaultArticles()
    {
        using var connection =
            CreateConnection();

        connection.Open();

        using var countCommand =
            connection.CreateCommand();

        countCommand.CommandText =
            "SELECT COUNT(*) FROM Articles;";

        long count =
            Convert.ToInt64(
                countCommand.ExecuteScalar());

        if (count > 0)
            return;

        AddOfficialArticle(
            connection,
            "Скайрим",
            "Лор Skyrim",
            "Северная провинция Империи, известная как родина нордов.",
            """
            Скайрим — одна из провинций Тамриэля.

            Земля отличается суровым климатом, горными массивами,
            древними руинами и многочисленными поселениями.

            В регионе проживают норды и представители множества
            других народов Тамриэля.

            Скайрим имеет большое значение для истории Второй эры
            и последующих событий.
            """,
            "Скайрим, Нирн, Тамриэль",
            "Лор");

        AddOfficialArticle(
            connection,
            "Темное Братство",
            "Фракции",
            "Тайное братство ассасинов, почитающее Ситиса.",
            """
            Темное Братство — тайная организация убийц.

            Члены Братства выполняют контракты на убийство,
            заключенные через систему Черной руки.

            Центральное место в традициях Братства занимает
            поклонение Ситису и почитание Матери Ночи.
            """,
            "Братство, ассасины, Ситис",
            "Фракция");

        AddOfficialArticle(
            connection,
            "Вайтран",
            "Локации",
            "Город в центральной части Скайрима.",
            """
            Вайтран расположен в центральной части Скайрима,
            на обширной равнине.

            Город известен Драконьим Пределом, рынком,
            кузницей и храмом Кинарет.

            В окрестностях Вайтрана находятся многочисленные
            фермы и дороги, связывающие город с другими
            регионами провинции.
            """,
            "Вайтран, Драконий Предел",
            "Локация");

        AddOfficialArticle(
            connection,
            "Ситис",
            "Боги и даэдра",
            "Таинственное божество, связанное с Темным Братством.",
            """
            Ситис занимает центральное место в религиозных
            представлениях Темного Братства.

            В традициях Братства он рассматривается как
            первичная сила пустоты и разрушения.

            Точное описание природы Ситиса различается
            в различных источниках и религиозных традициях.
            """,
            "Ситис, Темное Братство, Пустота",
            "Религия");
    }

    private void AddOfficialArticle(
        SqliteConnection connection,
        string title,
        string category,
        string summary,
        string content,
        string tags,
        string flags)
    {
        using var command =
            connection.CreateCommand();

        DateTime now =
            DateTime.Now;

        command.CommandText = """
            INSERT INTO Articles
            (
                Title,
                Category,
                Summary,
                Content,
                Tags,
                Flags,
                IsFavorite,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $title,
                $category,
                $summary,
                $content,
                $tags,
                $flags,
                0,
                $created,
                $updated
            );
            """;

        command.Parameters.AddWithValue(
            "$title",
            title);

        command.Parameters.AddWithValue(
            "$category",
            category);

        command.Parameters.AddWithValue(
            "$summary",
            summary);

        command.Parameters.AddWithValue(
            "$content",
            content);

        command.Parameters.AddWithValue(
            "$tags",
            tags);

        command.Parameters.AddWithValue(
            "$flags",
            flags);

        command.Parameters.AddWithValue(
            "$created",
            now.ToString("O"));

        command.Parameters.AddWithValue(
            "$updated",
            now.ToString("O"));

        command.ExecuteNonQuery();
    }
}
