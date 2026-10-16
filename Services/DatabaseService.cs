using System.IO;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public class DatabaseService
{
    public const string DatabasePathEnvironmentVariable = "SKYRIM_RP_USER_DB";

    private readonly string _databasePath;

    public DatabaseService(string? databasePath = null)
    {
        _databasePath = ResolveDatabasePath(databasePath);
        UserDatabasePreflight.ValidateExisting(_databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        InitializeDatabase();
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
            "user.db");
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
            SourceIsOfficial INTEGER NOT NULL DEFAULT 0,
            TargetArticleId INTEGER NOT NULL,
            TargetIsOfficial INTEGER NOT NULL DEFAULT 0,
            LinkType TEXT NOT NULL DEFAULT '',
            Description TEXT
        );

        CREATE TABLE IF NOT EXISTS RpEvents
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            EventType TEXT NOT NULL,
            Location TEXT,
            Faction TEXT,
            Characters TEXT,
            Objective TEXT,
            Conditions TEXT,
            Reward TEXT,
            Extra TEXT,
            GeneratedText TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ArticleSources
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ArticleId INTEGER NOT NULL,
            ArticleIsOfficial INTEGER NOT NULL DEFAULT 0,
            Title TEXT NOT NULL,
            Url TEXT NOT NULL,
            SourceType TEXT NOT NULL DEFAULT 'Website',
            Author TEXT,
            Description TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_ArticleSources_Article
        ON ArticleSources (ArticleId, ArticleIsOfficial);

        CREATE TABLE IF NOT EXISTS ArticleNotes
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ArticleId INTEGER NOT NULL,
            ArticleIsOfficial INTEGER NOT NULL DEFAULT 0,
            NoteType TEXT NOT NULL DEFAULT 'Private',
            Content TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_ArticleNotes_Article
        ON ArticleNotes (ArticleId, ArticleIsOfficial);

        CREATE TABLE IF NOT EXISTS Categories
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Description TEXT,
            IsSystem INTEGER NOT NULL DEFAULT 0,
            SortOrder INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Tags
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ArticleTags
        (
            ArticleId INTEGER NOT NULL,
            TagId INTEGER NOT NULL,
            PRIMARY KEY (ArticleId, TagId)
        );

        CREATE TABLE IF NOT EXISTS Flags
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Icon TEXT,
            Color TEXT,
            Description TEXT,
            IsSystem INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Favorites
        (
            ArticleId INTEGER NOT NULL,
            ArticleIsOfficial INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (ArticleId, ArticleIsOfficial)
        );

                        CREATE TABLE IF NOT EXISTS ViewingHistory
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ArticleId INTEGER NOT NULL,
            ArticleIsOfficial INTEGER NOT NULL DEFAULT 0,
            OpenedAt TEXT NOT NULL,
            ViewedAt TEXT NOT NULL,
            UNIQUE (ArticleId, ArticleIsOfficial)
        );

        CREATE INDEX IF NOT EXISTS IX_ViewingHistory_Article
        ON ViewingHistory (ArticleId, ArticleIsOfficial);
        """;

    command.ExecuteNonQuery();

    using (var migrationCommand = connection.CreateCommand())
    {
        migrationCommand.CommandText = """
            ALTER TABLE ViewingHistory
            ADD COLUMN ViewedAt TEXT;
            """;

        try
        {
            migrationCommand.ExecuteNonQuery();
        }
        catch (SqliteException ex)
        {
            if (!ex.Message.Contains(
                    "duplicate column name",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }
        }
    }

    using (var indexCommand = connection.CreateCommand())
    {
        indexCommand.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_ViewingHistory_ViewedAt
            ON ViewingHistory (ViewedAt);
            """;

        indexCommand.ExecuteNonQuery();
    }
}

    // =========================================================
    // СТАТЬИ
    // =========================================================

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
            ORDER BY Title COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            articles.Add(
                new Article
                {
                    Id = reader.GetInt32(0),
                    Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Category = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Summary = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Content = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Tags = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Flags = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    IsFavorite = reader.GetInt32(7) != 0,
                    IsOfficial = false,

                    CreatedAt =
                        DateTime.TryParse(
                            reader.GetString(8),
                            out var createdAt)
                            ? createdAt
                            : DateTime.Now,

                    UpdatedAt =
                        DateTime.TryParse(
                            reader.GetString(9),
                            out var updatedAt)
                            ? updatedAt
                            : DateTime.Now
                });
        }

        return articles;
    }

    public int AddArticle(Article article)
    {
        if(article.IsOfficial || article.IsMvp) throw new InvalidOperationException("Нельзя записать статью read-only источника в личную базу.");
        using var connection = CreateConnection();

        connection.Open();

        using var transaction=connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction=transaction;

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
                $isFavorite,
                $createdAt,
                $updatedAt
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", article.Title);
        command.Parameters.AddWithValue("$category", article.Category);
        command.Parameters.AddWithValue("$summary", article.Summary);
        command.Parameters.AddWithValue("$content", article.Content);
        command.Parameters.AddWithValue("$tags", article.Tags);
        command.Parameters.AddWithValue("$flags", article.Flags);
        command.Parameters.AddWithValue(
            "$isFavorite",
            article.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue(
            "$createdAt",
            article.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedAt",
            article.UpdatedAt.ToString("O"));

        var id=Convert.ToInt32(command.ExecuteScalar());
        SyncArticleMetadata(connection,transaction,id,article);
        transaction.Commit();
        return id;
    }

    public void UpdateArticle(Article article)
    {
        if(article.IsOfficial || article.IsMvp) throw new InvalidOperationException("Нельзя изменить статью read-only источника через личную базу.");
        using var connection = CreateConnection();

        connection.Open();

        using var transaction=connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction=transaction;

        command.CommandText = """
            UPDATE Articles
            SET
                Title = $title,
                Category = $category,
                Summary = $summary,
                Content = $content,
                Tags = $tags,
                Flags = $flags,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", article.Id);
        command.Parameters.AddWithValue("$title", article.Title);
        command.Parameters.AddWithValue("$category", article.Category);
        command.Parameters.AddWithValue("$summary", article.Summary);
        command.Parameters.AddWithValue("$content", article.Content);
        command.Parameters.AddWithValue("$tags", article.Tags);
        command.Parameters.AddWithValue("$flags", article.Flags);
        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.Now.ToString("O"));

        if(command.ExecuteNonQuery()!=1) throw new InvalidOperationException("Статья для обновления не найдена.");
        SyncArticleMetadata(connection,transaction,article.Id,article);
        transaction.Commit();
    }

    private static void SyncArticleMetadata(SqliteConnection c,SqliteTransaction tx,int id,Article article)
    {
        void Execute(string sql,params (string Name,object Value)[] parameters)
        {using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;foreach(var p in parameters)cmd.Parameters.AddWithValue(p.Name,p.Value);cmd.ExecuteNonQuery();}
        Execute("INSERT OR IGNORE INTO Categories(Name) VALUES($name)",("$name",article.Category));
        Execute("DELETE FROM ArticleTags WHERE ArticleId=$id",("$id",id));
        foreach(var tag in ArticleFilterRules.Tokens(article.Tags))
        {
            Execute("INSERT OR IGNORE INTO Tags(Name,CreatedAt) VALUES($name,$date)",("$name",tag),("$date",DateTime.Now.ToString("O")));
            Execute("INSERT OR IGNORE INTO ArticleTags(ArticleId,TagId) SELECT $id,Id FROM Tags WHERE Name=$name",("$id",id),("$name",tag));
        }
        foreach(var flag in ArticleFilterRules.Tokens(article.Flags))
            Execute("INSERT OR IGNORE INTO Flags(Name) VALUES($name)",("$name",flag));
    }

    private static void PropagateMetadataName(SqliteConnection c,SqliteTransaction tx,string field,string oldName,string name)
    {
        if(field is not ("Category" or "Flags")) throw new ArgumentException("Неизвестное поле.");
        if(name.Length==0 || (field=="Flags" && name.IndexOfAny([',',';','\r','\n'])>=0)) throw new ArgumentException("Некорректное название метаданных.");
        if(oldName==name) return;
        var changes=new List<(int Id,string Value)>();
        using(var read=c.CreateCommand())
        {
            read.Transaction=tx;read.CommandText=$"SELECT Id,COALESCE({field},'') FROM Articles";
            using var rows=read.ExecuteReader();
            while(rows.Read())
            {
                var value=rows.GetString(1);
                if(field=="Category" && value.Equals(oldName,StringComparison.OrdinalIgnoreCase)) changes.Add((rows.GetInt32(0),name));
                else if(field=="Flags")
                {
                    var values=ArticleFilterRules.Tokens(value);
                    if(values.Contains(oldName,StringComparer.OrdinalIgnoreCase)) changes.Add((rows.GetInt32(0),string.Join(", ",values.Select(v=>v.Equals(oldName,StringComparison.OrdinalIgnoreCase)?name:v).Distinct(StringComparer.OrdinalIgnoreCase))));
                }
            }
        }
        foreach(var change in changes)
        {using var update=c.CreateCommand();update.Transaction=tx;update.CommandText=$"UPDATE Articles SET {field}=$value WHERE Id=$id";update.Parameters.AddWithValue("$value",change.Value);update.Parameters.AddWithValue("$id",change.Id);update.ExecuteNonQuery();}
    }

    public void DeleteArticle(int id)
{
    using var connection = CreateConnection();

    connection.Open();

    using var transaction = connection.BeginTransaction();

    using var deleteLinksCommand = connection.CreateCommand();

    deleteLinksCommand.Transaction = transaction;

    deleteLinksCommand.CommandText = """
        DELETE FROM Links
        WHERE
            (
                SourceArticleId = $id
                AND SourceIsOfficial = 0
            )
            OR
            (
                TargetArticleId = $id
                AND TargetIsOfficial = 0
            );
        """;

    deleteLinksCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteLinksCommand.ExecuteNonQuery();

    using var deleteSourcesCommand = connection.CreateCommand();

    deleteSourcesCommand.Transaction = transaction;

    deleteSourcesCommand.CommandText = """
        DELETE FROM ArticleSources
        WHERE
            ArticleId = $id
            AND ArticleIsOfficial = 0;
        """;

    deleteSourcesCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteSourcesCommand.ExecuteNonQuery();

    using var deleteNotesCommand = connection.CreateCommand();

    deleteNotesCommand.Transaction = transaction;

    deleteNotesCommand.CommandText = """
        DELETE FROM ArticleNotes
        WHERE
            ArticleId = $id
            AND ArticleIsOfficial = 0;
        """;

    deleteNotesCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteNotesCommand.ExecuteNonQuery();

    using var deleteTagsCommand = connection.CreateCommand();

    deleteTagsCommand.Transaction = transaction;

    deleteTagsCommand.CommandText = """
        DELETE FROM ArticleTags
        WHERE ArticleId = $id;
        """;

    deleteTagsCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteTagsCommand.ExecuteNonQuery();

    using var deleteFavoriteCommand = connection.CreateCommand();

    deleteFavoriteCommand.Transaction = transaction;

    deleteFavoriteCommand.CommandText = """
        DELETE FROM Favorites
        WHERE
            ArticleId = $id
            AND ArticleIsOfficial = 0;
        """;

    deleteFavoriteCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteFavoriteCommand.ExecuteNonQuery();

    using var deleteHistoryCommand = connection.CreateCommand();

    deleteHistoryCommand.Transaction = transaction;

    deleteHistoryCommand.CommandText = """
        DELETE FROM ViewingHistory
        WHERE
            ArticleId = $id
            AND ArticleIsOfficial = 0;
        """;

    deleteHistoryCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteHistoryCommand.ExecuteNonQuery();

    using var deleteArticleCommand = connection.CreateCommand();

    deleteArticleCommand.Transaction = transaction;

    deleteArticleCommand.CommandText = """
        DELETE FROM Articles
        WHERE Id = $id;
        """;

    deleteArticleCommand.Parameters.AddWithValue(
        "$id",
        id);

    deleteArticleCommand.ExecuteNonQuery();

    transaction.Commit();
}

    public void SetFavorite(int id, bool isFavorite)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE Articles
            SET IsFavorite = $favorite
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);

        command.Parameters.AddWithValue(
            "$favorite",
            isFavorite ? 1 : 0);

        command.ExecuteNonQuery();
    }

    // =========================================================
    // СВЯЗИ
    // =========================================================

    public bool LinkExists(
        int sourceArticleId,
        bool sourceIsOfficial,
        int targetArticleId,
        bool targetIsOfficial,
        int? excludeLinkId = null)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*)
            FROM Links
            WHERE
            (
                (
                    SourceArticleId = $sourceArticleId
                    AND SourceIsOfficial = $sourceIsOfficial
                    AND TargetArticleId = $targetArticleId
                    AND TargetIsOfficial = $targetIsOfficial
                )
                OR
                (
                    SourceArticleId = $targetArticleId
                    AND SourceIsOfficial = $targetIsOfficial
                    AND TargetArticleId = $sourceArticleId
                    AND TargetIsOfficial = $sourceIsOfficial
                )
            )
            AND
            (
                $excludeLinkId IS NULL
                OR Id != $excludeLinkId
            );
            """;

        command.Parameters.AddWithValue(
            "$sourceArticleId",
            sourceArticleId);

        command.Parameters.AddWithValue(
            "$sourceIsOfficial",
            sourceIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$targetArticleId",
            targetArticleId);

        command.Parameters.AddWithValue(
            "$targetIsOfficial",
            targetIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$excludeLinkId",
            excludeLinkId.HasValue
                ? excludeLinkId.Value
                : DBNull.Value);

        long count =
            Convert.ToInt64(
                command.ExecuteScalar());

        return count > 0;
    }

    public void AddLink(ArticleLink link)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

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
                $sourceArticleId,
                $sourceIsOfficial,
                $targetArticleId,
                $targetIsOfficial,
                $linkType,
                $description
            );
            """;

        command.Parameters.AddWithValue(
            "$sourceArticleId",
            link.SourceArticleId);

        command.Parameters.AddWithValue(
            "$sourceIsOfficial",
            link.SourceIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$targetArticleId",
            link.TargetArticleId);

        command.Parameters.AddWithValue(
            "$targetIsOfficial",
            link.TargetIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$linkType",
            link.LinkType);

        command.Parameters.AddWithValue(
            "$description",
            link.Description);

        command.ExecuteNonQuery();
    }

    public void UpdateLink(ArticleLink link)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE Links
            SET
                SourceArticleId = $sourceArticleId,
                SourceIsOfficial = $sourceIsOfficial,
                TargetArticleId = $targetArticleId,
                TargetIsOfficial = $targetIsOfficial,
                LinkType = $linkType,
                Description = $description
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", link.Id);
        command.Parameters.AddWithValue(
            "$sourceArticleId",
            link.SourceArticleId);
        command.Parameters.AddWithValue(
            "$sourceIsOfficial",
            link.SourceIsOfficial ? 1 : 0);
        command.Parameters.AddWithValue(
            "$targetArticleId",
            link.TargetArticleId);
        command.Parameters.AddWithValue(
            "$targetIsOfficial",
            link.TargetIsOfficial ? 1 : 0);
        command.Parameters.AddWithValue(
            "$linkType",
            link.LinkType);
        command.Parameters.AddWithValue(
            "$description",
            link.Description);

        command.ExecuteNonQuery();
    }

    public void DeleteLink(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            DELETE FROM Links
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);

        command.ExecuteNonQuery();
    }

    public List<ArticleLink> GetLinksForArticle(
        int articleId,
        bool isOfficial)
    {
        var links = new List<ArticleLink>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

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

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            links.Add(
                new ArticleLink
                {
                    Id = reader.GetInt32(0),
                    SourceArticleId = reader.GetInt32(1),
                    SourceIsOfficial = reader.GetInt32(2) != 0,
                    TargetArticleId = reader.GetInt32(3),
                    TargetIsOfficial = reader.GetInt32(4) != 0,
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


    public void DeleteSource(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            DELETE FROM ArticleSources
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id);

        command.ExecuteNonQuery();
    }

        // =========================================================
       // ИСТОЧНИКИ СТАТЕЙ
      // =========================================================

    public List<ArticleSource> GetSourcesForArticle(
    int articleId,
    bool isOfficial)
{
    var sources = new List<ArticleSource>();

    using var connection = CreateConnection();

    connection.Open();

    using var command = connection.CreateCommand();

    command.CommandText = """
        SELECT
            Id,
            ArticleId,
            ArticleIsOfficial,
            Title,
            Url,
            SourceType,
            Author,
            Description,
            CreatedAt,
            UpdatedAt
        FROM ArticleSources
        WHERE
            ArticleId = $articleId
            AND ArticleIsOfficial = $isOfficial
        ORDER BY Id;
        """;

    command.Parameters.AddWithValue(
        "$articleId",
        articleId);

    command.Parameters.AddWithValue(
        "$isOfficial",
        isOfficial ? 1 : 0);

    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        DateTime createdAt = DateTime.Now;

        if (!reader.IsDBNull(8))
        {
            var value = reader.GetString(8);

            if (DateTime.TryParse(value, out var parsedDate))
            {
                createdAt = parsedDate;
            }
        }

        DateTime updatedAt = createdAt;

        if (!reader.IsDBNull(9))
        {
            var value = reader.GetString(9);

            if (DateTime.TryParse(value, out var parsedDate))
            {
                updatedAt = parsedDate;
            }
        }

        sources.Add(
            new ArticleSource
            {
                Id = reader.GetInt32(0),

                ArticleId = reader.GetInt32(1),

                ArticleIsOfficial =
                    reader.GetInt32(2) != 0,

                Title =
                    reader.IsDBNull(3)
                        ? ""
                        : reader.GetString(3),

                Url =
                    reader.IsDBNull(4)
                        ? ""
                        : reader.GetString(4),

                SourceType =
                    reader.IsDBNull(5)
                        ? "Website"
                        : reader.GetString(5),

                Author =
                    reader.IsDBNull(6)
                        ? ""
                        : reader.GetString(6),

                Description =
                    reader.IsDBNull(7)
                        ? ""
                        : reader.GetString(7),

                CreatedAt = createdAt,

                UpdatedAt = updatedAt
            });
    }

    return sources;
}

public List<ArticleSource> GetArticleSources(
    int articleId,
    bool articleIsOfficial = false)
{
    var sources =
        new List<ArticleSource>();

    using var connection =
        CreateConnection();

    connection.Open();

    using var command =
        connection.CreateCommand();

    command.CommandText = """
        SELECT
            Id,
            ArticleId,
            ArticleIsOfficial,
            Title,
            Url,
            SourceType,
            Author,
            Description,
            CreatedAt,
            UpdatedAt
        FROM ArticleSources
        WHERE
            ArticleId = $articleId
            AND ArticleIsOfficial = $articleIsOfficial
        ORDER BY Id;
        """;

    command.Parameters.AddWithValue(
        "$articleId",
        articleId);

    command.Parameters.AddWithValue(
        "$articleIsOfficial",
        articleIsOfficial ? 1 : 0);

    using var reader =
        command.ExecuteReader();

    while (reader.Read())
    {
        DateTime createdAt =
            DateTime.TryParse(
                reader.GetString(8),
                out var parsedCreatedAt)
                ? parsedCreatedAt
                : DateTime.Now;

        DateTime updatedAt =
            DateTime.TryParse(
                reader.GetString(9),
                out var parsedUpdatedAt)
                ? parsedUpdatedAt
                : DateTime.Now;

        sources.Add(
            new ArticleSource
            {
                Id =
                    reader.GetInt32(0),

                ArticleId =
                    reader.GetInt32(1),

                ArticleIsOfficial =
                    reader.GetInt32(2) != 0,

                Title =
                    reader.IsDBNull(3)
                        ? ""
                        : reader.GetString(3),

                Url =
                    reader.IsDBNull(4)
                        ? ""
                        : reader.GetString(4),

                SourceType =
                    reader.IsDBNull(5)
                        ? "Website"
                        : reader.GetString(5),

                Author =
                    reader.IsDBNull(6)
                        ? ""
                        : reader.GetString(6),

                Description =
                    reader.IsDBNull(7)
                        ? ""
                        : reader.GetString(7),

                CreatedAt =
                    createdAt,

                UpdatedAt =
                    updatedAt
            });
    }

    return sources;
}

    public int AddSource(ArticleSource source)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO ArticleSources
            (
                ArticleId,
                ArticleIsOfficial,
                Title,
                Url,
                SourceType,
                Author,
                Description,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $articleId,
                $articleIsOfficial,
                $title,
                $url,
                $sourceType,
                $author,
                $description,
                $createdAt,
                $updatedAt
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$articleId",
            source.ArticleId);

        command.Parameters.AddWithValue(
            "$articleIsOfficial",
            source.ArticleIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$title",
            source.Title);

        command.Parameters.AddWithValue(
            "$url",
            source.Url);

        command.Parameters.AddWithValue(
            "$sourceType",
            source.SourceType);

        command.Parameters.AddWithValue(
            "$author",
            source.Author);

        command.Parameters.AddWithValue(
            "$description",
            source.Description);

        command.Parameters.AddWithValue(
            "$createdAt",
            source.CreatedAt.ToString("O"));

        command.Parameters.AddWithValue(
            "$updatedAt",
            source.UpdatedAt.ToString("O"));

        return Convert.ToInt32(
            command.ExecuteScalar());
    }


    public void UpdateSource(ArticleSource source)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE ArticleSources
            SET
                ArticleId = $articleId,
                ArticleIsOfficial = $articleIsOfficial,
                Title = $title,
                Url = $url,
                SourceType = $sourceType,
                Author = $author,
                Description = $description,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            source.Id);

        command.Parameters.AddWithValue(
            "$articleId",
            source.ArticleId);

        command.Parameters.AddWithValue(
            "$articleIsOfficial",
            source.ArticleIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$title",
            source.Title);

        command.Parameters.AddWithValue(
            "$url",
            source.Url);

        command.Parameters.AddWithValue(
            "$sourceType",
            source.SourceType);

        command.Parameters.AddWithValue(
            "$author",
            source.Author);

        command.Parameters.AddWithValue(
            "$description",
            source.Description);

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.Now.ToString("O"));

        command.ExecuteNonQuery();
    }

    // =========================================================
    // ЗАМЕТКИ СТАТЕЙ
    // =========================================================

    public List<ArticleNote> GetArticleNotes(
        int articleId,
        bool isOfficial)
    {
        var notes = new List<ArticleNote>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                ArticleId,
                ArticleIsOfficial,
                NoteType,
                Content,
                CreatedAt,
                UpdatedAt
            FROM ArticleNotes
            WHERE
                ArticleId = $articleId
                AND ArticleIsOfficial = $isOfficial
            ORDER BY CreatedAt DESC;
            """;

        command.Parameters.AddWithValue(
            "$articleId",
            articleId);

        command.Parameters.AddWithValue(
            "$isOfficial",
            isOfficial ? 1 : 0);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            notes.Add(
                new ArticleNote
                {
                    Id = reader.GetInt32(0),
                    ArticleId = reader.GetInt32(1),
                    ArticleIsOfficial = reader.GetInt32(2) != 0,

                    NoteType =
                        reader.IsDBNull(3)
                            ? "Private"
                            : reader.GetString(3),

                    Content =
                        reader.IsDBNull(4)
                            ? ""
                            : reader.GetString(4),

                    CreatedAt =
                        DateTime.TryParse(
                            reader.GetString(5),
                            out var createdAt)
                            ? createdAt
                            : DateTime.Now,

                    UpdatedAt =
                        DateTime.TryParse(
                            reader.GetString(6),
                            out var updatedAt)
                            ? updatedAt
                            : DateTime.Now
                });
        }

        return notes;
    }

    public int AddArticleNote(ArticleNote note)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO ArticleNotes
            (
                ArticleId,
                ArticleIsOfficial,
                NoteType,
                Content,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $articleId,
                $articleIsOfficial,
                $noteType,
                $content,
                $createdAt,
                $updatedAt
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$articleId",
            note.ArticleId);

        command.Parameters.AddWithValue(
            "$articleIsOfficial",
            note.ArticleIsOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$noteType",
            note.NoteType);

        command.Parameters.AddWithValue(
            "$content",
            note.Content);

        command.Parameters.AddWithValue(
            "$createdAt",
            note.CreatedAt.ToString("O"));

        command.Parameters.AddWithValue(
            "$updatedAt",
            note.UpdatedAt.ToString("O"));

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    public void UpdateArticleNote(ArticleNote note)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE ArticleNotes
            SET
                NoteType = $noteType,
                Content = $content,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            note.Id);

        command.Parameters.AddWithValue(
            "$noteType",
            note.NoteType);

        command.Parameters.AddWithValue(
            "$content",
            note.Content);

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.Now.ToString("O"));

        if(command.ExecuteNonQuery()!=1) throw new InvalidOperationException("Запись для обновления не найдена.");
    }

    public void DeleteArticleNote(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            DELETE FROM ArticleNotes
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id);

        command.ExecuteNonQuery();
    }


    // =========================================================
    // КАТЕГОРИИ
    // =========================================================

    public List<ContentCategory> GetCategories()
    {
        var categories = new List<ContentCategory>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                Name,
                Description,
                IsSystem,
                SortOrder
            FROM Categories
            ORDER BY SortOrder, Name COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            categories.Add(
                new ContentCategory
                {
                    Id = reader.GetInt32(0),

                    Name =
                        reader.IsDBNull(1)
                            ? ""
                            : reader.GetString(1),

                    Description =
                        reader.IsDBNull(2)
                            ? ""
                            : reader.GetString(2),

                    IsSystem =
                        reader.GetInt32(3) != 0,

                    SortOrder =
                        reader.GetInt32(4)
                });
        }

        return categories;
    }

    public int AddCategory(ContentCategory category)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO Categories
            (
                Name,
                Description,
                IsSystem,
                SortOrder
            )
            VALUES
            (
                $name,
                $description,
                $isSystem,
                $sortOrder
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$name",
            category.Name.Trim());

        command.Parameters.AddWithValue(
            "$description",
            category.Description ?? "");

        command.Parameters.AddWithValue(
            "$isSystem",
            category.IsSystem ? 1 : 0);

        command.Parameters.AddWithValue(
            "$sortOrder",
            category.SortOrder);

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    public void UpdateCategory(ContentCategory category)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var transaction=connection.BeginTransaction();
        using var previous=connection.CreateCommand(); previous.Transaction=transaction;
        previous.CommandText="SELECT Name FROM Categories WHERE Id=$id"; previous.Parameters.AddWithValue("$id",category.Id);
        var oldName=previous.ExecuteScalar() as string ?? throw new InvalidOperationException("Запись метаданных не найдена.");
        using var command = connection.CreateCommand(); command.Transaction=transaction;

        command.CommandText = """
            UPDATE Categories
            SET
                Name = $name,
                Description = $description,
                SortOrder = $sortOrder
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            category.Id);

        command.Parameters.AddWithValue(
            "$name",
            category.Name.Trim());

        command.Parameters.AddWithValue(
            "$description",
            category.Description ?? "");

        command.Parameters.AddWithValue(
            "$sortOrder",
            category.SortOrder);

        command.ExecuteNonQuery();
        PropagateMetadataName(connection,transaction,"Category",oldName,category.Name.Trim());
        transaction.Commit();
    }

    public void DeleteCategory(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            DELETE FROM Categories
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id);

        command.ExecuteNonQuery();
    }


    // =========================================================
    // ТЕГИ
    // =========================================================

    public List<ContentTag> GetTags()
    {
        var tags = new List<ContentTag>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                t.Id,
                t.Name,
                t.CreatedAt,
                COUNT(at.ArticleId) AS UsageCount
            FROM Tags t
            LEFT JOIN ArticleTags at
                ON at.TagId = t.Id
            GROUP BY
                t.Id,
                t.Name,
                t.CreatedAt
            ORDER BY t.Name COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            tags.Add(
                new ContentTag
                {
                    Id = reader.GetInt32(0),

                    Name =
                        reader.IsDBNull(1)
                            ? ""
                            : reader.GetString(1),

                    CreatedAt =
                        DateTime.TryParse(
                            reader.GetString(2),
                            out var createdAt)
                            ? createdAt
                            : DateTime.Now,

                    UsageCount =
                        Convert.ToInt32(reader.GetInt64(3))
                });
        }

        return tags;
    }

// =========================================================
// ИМПОРТ
// =========================================================

public Article? FindArticleByTitle(
    string title)
{
    using var connection = CreateConnection();

    connection.Open();

    using var command =
        connection.CreateCommand();

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
        WHERE Title = $title
        LIMIT 1;
        """;

    command.Parameters.AddWithValue(
        "$title",
        title.Trim());

    using var reader =
        command.ExecuteReader();

    if (!reader.Read())
        return null;

    return new Article
    {
        Id =
            reader.GetInt32(0),

        Title =
            reader.IsDBNull(1)
                ? ""
                : reader.GetString(1),

        Category =
            reader.IsDBNull(2)
                ? ""
                : reader.GetString(2),

        Summary =
            reader.IsDBNull(3)
                ? ""
                : reader.GetString(3),

        Content =
            reader.IsDBNull(4)
                ? ""
                : reader.GetString(4),

        Tags =
            reader.IsDBNull(5)
                ? ""
                : reader.GetString(5),

        Flags =
            reader.IsDBNull(6)
                ? ""
                : reader.GetString(6),

        IsFavorite =
            reader.GetInt32(7) != 0,

        IsOfficial =
            false,

        CreatedAt =
            DateTime.TryParse(
                reader.GetString(8),
                out var createdAt)
                ? createdAt
                : DateTime.Now,

        UpdatedAt =
            DateTime.TryParse(
                reader.GetString(9),
                out var updatedAt)
                ? updatedAt
                : DateTime.Now
    };
}

public int GetOrCreateCategory(
    string name,
    string description = "",
    int sortOrder = 0)
{
    name = name.Trim();

    using var connection = CreateConnection();

    connection.Open();

    using var command = connection.CreateCommand();

    command.CommandText = """
        SELECT Id
        FROM Categories
        WHERE Name = $name
        LIMIT 1;
        """;

    command.Parameters.AddWithValue(
        "$name",
        name);

    var existing =
        command.ExecuteScalar();

    if (existing != null &&
        existing != DBNull.Value)
    {
        return Convert.ToInt32(existing);
    }

    command.Parameters.Clear();

    command.CommandText = """
        INSERT INTO Categories
        (
            Name,
            Description,
            IsSystem,
            SortOrder
        )
        VALUES
        (
            $name,
            $description,
            0,
            $sortOrder
        );

        SELECT last_insert_rowid();
        """;

    command.Parameters.AddWithValue(
        "$name",
        name);

    command.Parameters.AddWithValue(
        "$description",
        description ?? "");

    command.Parameters.AddWithValue(
        "$sortOrder",
        sortOrder);

    return Convert.ToInt32(
        command.ExecuteScalar());
}

public int GetOrCreateTag(
    string name)
{
    name = name.Trim();

    using var connection = CreateConnection();

    connection.Open();

    using var command = connection.CreateCommand();

    command.CommandText = """
        SELECT Id
        FROM Tags
        WHERE Name = $name
        LIMIT 1;
        """;

    command.Parameters.AddWithValue(
        "$name",
        name);

    var existing =
        command.ExecuteScalar();

    if (existing != null &&
        existing != DBNull.Value)
    {
        return Convert.ToInt32(existing);
    }

    command.Parameters.Clear();

    command.CommandText = """
        INSERT INTO Tags
        (
            Name,
            CreatedAt
        )
        VALUES
        (
            $name,
            $createdAt
        );

        SELECT last_insert_rowid();
        """;

    command.Parameters.AddWithValue(
        "$name",
        name);

    command.Parameters.AddWithValue(
        "$createdAt",
        DateTime.Now.ToString("O"));

    return Convert.ToInt32(
        command.ExecuteScalar());
}

public void AddArticleTag(
    int articleId,
    int tagId)
{
    using var connection = CreateConnection();

    connection.Open();

    using var command = connection.CreateCommand();

    command.CommandText = """
        INSERT OR IGNORE INTO ArticleTags
        (
            ArticleId,
            TagId
        )
        VALUES
        (
            $articleId,
            $tagId
        );
        """;

    command.Parameters.AddWithValue(
        "$articleId",
        articleId);

    command.Parameters.AddWithValue(
        "$tagId",
        tagId);

    command.ExecuteNonQuery();
}

public void SetArticleTags(
    int articleId,
    IEnumerable<string> tagNames)
{
    using var connection = CreateConnection();

    connection.Open();

    using var transaction =
        connection.BeginTransaction();

    using var deleteCommand =
        connection.CreateCommand();

    deleteCommand.Transaction =
        transaction;

    deleteCommand.CommandText = """
        DELETE FROM ArticleTags
        WHERE ArticleId = $articleId;
        """;

    deleteCommand.Parameters.AddWithValue(
        "$articleId",
        articleId);

    deleteCommand.ExecuteNonQuery();

    foreach (
        var rawName in tagNames)
    {
        var name =
            rawName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            continue;

        using var tagCommand =
            connection.CreateCommand();

        tagCommand.Transaction =
            transaction;

        tagCommand.CommandText = """
            SELECT Id
            FROM Tags
            WHERE Name = $name
            LIMIT 1;
            """;

        tagCommand.Parameters.AddWithValue(
            "$name",
            name);

        var result =
            tagCommand.ExecuteScalar();

        int tagId;

        if (result != null &&
            result != DBNull.Value)
        {
            tagId =
                Convert.ToInt32(result);
        }
        else
        {
            tagCommand.Parameters.Clear();

            tagCommand.CommandText = """
                INSERT INTO Tags
                (
                    Name,
                    CreatedAt
                )
                VALUES
                (
                    $name,
                    $createdAt
                );

                SELECT last_insert_rowid();
                """;

            tagCommand.Parameters.AddWithValue(
                "$name",
                name);

            tagCommand.Parameters.AddWithValue(
                "$createdAt",
                DateTime.Now.ToString("O"));

            tagId =
                Convert.ToInt32(
                    tagCommand.ExecuteScalar());
        }

        using var linkCommand =
            connection.CreateCommand();

        linkCommand.Transaction =
            transaction;

        linkCommand.CommandText = """
            INSERT OR IGNORE INTO ArticleTags
            (
                ArticleId,
                TagId
            )
            VALUES
            (
                $articleId,
                $tagId
            );
            """;

        linkCommand.Parameters.AddWithValue(
            "$articleId",
            articleId);

        linkCommand.Parameters.AddWithValue(
            "$tagId",
            tagId);

        linkCommand.ExecuteNonQuery();
    }

    transaction.Commit();
}

    public int AddTag(string name)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO Tags
            (
                Name,
                CreatedAt
            )
            VALUES
            (
                $name,
                $createdAt
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$name",
            name.Trim());

        command.Parameters.AddWithValue(
            "$createdAt",
            DateTime.Now.ToString("O"));

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    public void RenameTag(int id, string name)
    {
        name=name.Trim();
        if(name.Length==0 || name.IndexOfAny([',',';','\r','\n'])>=0) throw new ArgumentException("Укажите одно непустое название тега без разделителей.");
        using var connection = CreateConnection();

        connection.Open();

        using var transaction=connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText="SELECT Name FROM Tags WHERE Id=$id"; command.Parameters.AddWithValue("$id",id);
        var oldName=command.ExecuteScalar() as string ?? throw new InvalidOperationException("Тег для переименования не найден.");
        if(oldName==name) {transaction.Commit();return;}

        command.CommandText = """
            UPDATE Tags
            SET Name = $name
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$name",
            name.Trim());

        command.ExecuteNonQuery();
        var changes=new List<(int Id,string Tags)>();
        using(var read=connection.CreateCommand())
        {
            read.Transaction=transaction; read.CommandText="SELECT Id,COALESCE(Tags,'') FROM Articles";
            using var rows=read.ExecuteReader();
            while(rows.Read())
            {
                var values=ArticleFilterRules.Tokens(rows.GetString(1));
                if(values.Contains(oldName,StringComparer.OrdinalIgnoreCase)) changes.Add((rows.GetInt32(0),string.Join(", ",values.Select(v=>v.Equals(oldName,StringComparison.OrdinalIgnoreCase)?name:v).Distinct(StringComparer.OrdinalIgnoreCase))));
            }
        }
        foreach(var change in changes)
        {
            using var update=connection.CreateCommand(); update.Transaction=transaction;
            update.CommandText="UPDATE Articles SET Tags=$tags WHERE Id=$article; INSERT OR IGNORE INTO ArticleTags(ArticleId,TagId) VALUES($article,$tag)";
            update.Parameters.AddWithValue("$tags",change.Tags); update.Parameters.AddWithValue("$article",change.Id); update.Parameters.AddWithValue("$tag",id); update.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public List<ArticleNote> GetAllArticleNotes()
    {
        using var c=MvpKnowledgeDatabaseService.OpenReadOnlyConnection(_databasePath); c.Open();
        using var cmd=c.CreateCommand(); cmd.CommandTimeout=3;
        cmd.CommandText="SELECT Id,ArticleId,ArticleIsOfficial,NoteType,Content,CreatedAt,UpdatedAt FROM ArticleNotes ORDER BY UpdatedAt DESC,Id DESC";
        using var rows=cmd.ExecuteReader(); var result=new List<ArticleNote>();
        while(rows.Read()) result.Add(new ArticleNote
        {
            Id=rows.GetInt32(0),ArticleId=rows.GetInt32(1),ArticleIsOfficial=rows.GetInt32(2)!=0,
            NoteType=rows.IsDBNull(3)?"Private":rows.GetString(3),Content=rows.IsDBNull(4)?"":rows.GetString(4),
            CreatedAt=DateTime.TryParse(rows.GetString(5),out var created)?created:DateTime.MinValue,
            UpdatedAt=DateTime.TryParse(rows.GetString(6),out var updated)?updated:DateTime.MinValue
        });
        return result;
    }

    public void DeleteTag(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var deleteRelations = connection.CreateCommand();

        deleteRelations.Transaction = transaction;

        deleteRelations.CommandText = """
            DELETE FROM ArticleTags
            WHERE TagId = $id;
            """;

        deleteRelations.Parameters.AddWithValue(
            "$id",
            id);

        deleteRelations.ExecuteNonQuery();

        using var deleteTag = connection.CreateCommand();

        deleteTag.Transaction = transaction;

        deleteTag.CommandText = """
            DELETE FROM Tags
            WHERE Id = $id;
            """;

        deleteTag.Parameters.AddWithValue(
            "$id",
            id);

        deleteTag.ExecuteNonQuery();

        transaction.Commit();
    }


    // =========================================================
    // ФЛАГИ
    // =========================================================

    public List<ContentFlag> GetFlags()
    {
        var flags = new List<ContentFlag>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                f.Id,
                f.Name,
                f.Icon,
                f.Color,
                f.Description,
                f.IsSystem,
                COUNT(a.Id) AS UsageCount
            FROM Flags f
            LEFT JOIN Articles a
                ON (
                    ',' || COALESCE(a.Flags, '') || ','
                ) LIKE '%,' || f.Name || ',%'
            GROUP BY
                f.Id,
                f.Name,
                f.Icon,
                f.Color,
                f.Description,
                f.IsSystem
            ORDER BY f.Name COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            flags.Add(
                new ContentFlag
                {
                    Id = reader.GetInt32(0),

                    Name =
                        reader.IsDBNull(1)
                            ? ""
                            : reader.GetString(1),

                    Icon =
                        reader.IsDBNull(2)
                            ? ""
                            : reader.GetString(2),

                    Color =
                        reader.IsDBNull(3)
                            ? ""
                            : reader.GetString(3),

                    Description =
                        reader.IsDBNull(4)
                            ? ""
                            : reader.GetString(4),

                    IsSystem =
                        reader.GetInt32(5) != 0,

                    UsageCount =
                        Convert.ToInt32(reader.GetInt64(6))
                });
        }

        return flags;
    }

    public int AddFlag(ContentFlag flag)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO Flags
            (
                Name,
                Icon,
                Color,
                Description,
                IsSystem
            )
            VALUES
            (
                $name,
                $icon,
                $color,
                $description,
                $isSystem
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$name",
            flag.Name.Trim());

        command.Parameters.AddWithValue(
            "$icon",
            flag.Icon ?? "");

        command.Parameters.AddWithValue(
            "$color",
            flag.Color ?? "");

        command.Parameters.AddWithValue(
            "$description",
            flag.Description ?? "");

        command.Parameters.AddWithValue(
            "$isSystem",
            flag.IsSystem ? 1 : 0);

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    public void UpdateFlag(ContentFlag flag)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var transaction=connection.BeginTransaction();
        using var previous=connection.CreateCommand(); previous.Transaction=transaction;
        previous.CommandText="SELECT Name FROM Flags WHERE Id=$id"; previous.Parameters.AddWithValue("$id",flag.Id);
        var oldName=previous.ExecuteScalar() as string ?? throw new InvalidOperationException("Запись метаданных не найдена.");
        using var command = connection.CreateCommand(); command.Transaction=transaction;

        command.CommandText = """
            UPDATE Flags
            SET
                Name = $name,
                Icon = $icon,
                Color = $color,
                Description = $description
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            flag.Id);

        command.Parameters.AddWithValue(
            "$name",
            flag.Name.Trim());

        command.Parameters.AddWithValue(
            "$icon",
            flag.Icon ?? "");

        command.Parameters.AddWithValue(
            "$color",
            flag.Color ?? "");

        command.Parameters.AddWithValue(
            "$description",
            flag.Description ?? "");

        command.ExecuteNonQuery();
        PropagateMetadataName(connection,transaction,"Flags",oldName,flag.Name.Trim());
        transaction.Commit();
    }

    public void DeleteFlag(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            DELETE FROM Flags
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id);

        command.ExecuteNonQuery();
    }


    // =========================================================
    // ИЗБРАННОЕ
    // =========================================================

    public List<ArticleReference> GetPersonalFavorites()
    {
        var favorites = new List<ArticleReference>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                ArticleId,
                ArticleIsOfficial
            FROM Favorites
            ORDER BY ArticleId;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            favorites.Add(
                new ArticleReference
                {
                    ArticleId = reader.GetInt32(0),
                    ArticleIsOfficial = reader.GetInt32(1) != 0,
                    Timestamp = DateTime.Now
                });
        }

        return favorites;
    }

    public void SetPersonalFavorite(
        int articleId,
        bool isOfficial,
        bool isFavorite)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        if (isFavorite)
        {
            command.CommandText = """
                INSERT OR IGNORE INTO Favorites
                (
                    ArticleId,
                    ArticleIsOfficial
                )
                VALUES
                (
                    $articleId,
                    $isOfficial
                );
                """;
        }
        else
        {
            command.CommandText = """
                DELETE FROM Favorites
                WHERE
                    ArticleId = $articleId
                    AND ArticleIsOfficial = $isOfficial;
                """;
        }

        command.Parameters.AddWithValue(
            "$articleId",
            articleId);

        command.Parameters.AddWithValue(
            "$isOfficial",
            isOfficial ? 1 : 0);

        command.ExecuteNonQuery();
    }


    // =========================================================
    // ИСТОРИЯ ПРОСМОТРОВ
    // =========================================================

    public void RecordArticleView(
        int articleId,
        bool isOfficial,
        DateTime? openedAt = null)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
    INSERT INTO ViewingHistory
    (
        ArticleId,
        ArticleIsOfficial,
        OpenedAt,
        ViewedAt
    )
    VALUES
    (
        $articleId,
        $isOfficial,
        $viewedAt,
        $viewedAt
    )
    ON CONFLICT (ArticleId, ArticleIsOfficial)
    DO UPDATE SET
        OpenedAt = excluded.OpenedAt,
        ViewedAt = excluded.ViewedAt;
    """;

        command.Parameters.AddWithValue(
            "$articleId",
            articleId);

        command.Parameters.AddWithValue(
            "$isOfficial",
            isOfficial ? 1 : 0);

        command.Parameters.AddWithValue(
            "$viewedAt",
            (openedAt ?? DateTime.Now).ToString("O"));

        command.ExecuteNonQuery();
    }

    public List<ArticleReference> GetRecentArticles()
{
    var articles = new List<ArticleReference>();

    using var connection = CreateConnection();

    connection.Open();

    using var command = connection.CreateCommand();

    command.CommandText = """
        SELECT
            ArticleId,
            ArticleIsOfficial,
            COALESCE(ViewedAt, OpenedAt) AS LastViewedAt
        FROM ViewingHistory
        ORDER BY julianday(COALESCE(ViewedAt, OpenedAt)) DESC,
            ArticleIsOfficial ASC, ArticleId ASC
        LIMIT 20;
        """;

    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        DateTime viewedAt = DateTime.Now;

        if (!reader.IsDBNull(2))
        {
            var value = reader.GetString(2);

            if (DateTime.TryParse(value, out var parsedDate))
            {
                viewedAt = parsedDate;
            }
        }

        articles.Add(
            new ArticleReference
            {
                ArticleId = reader.GetInt32(0),
                ArticleIsOfficial = reader.GetInt32(1) != 0,
                Timestamp = viewedAt
            });
    }

    return articles;
}


    // =========================================================
    // RP-ИВЕНТЫ
    // =========================================================
    
    public List<RpEvent> GetRpEvents()
    {
        var events = new List<RpEvent>();

        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                Title,
                EventType,
                Location,
                Faction,
                Characters,
                Objective,
                Conditions,
                Reward,
                Extra,
                GeneratedText,
                CreatedAt,
                UpdatedAt
            FROM RpEvents
            ORDER BY CreatedAt DESC;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            events.Add(
                new RpEvent
                {
                    Id = reader.GetInt32(0),
                    Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    EventType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Location = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Faction = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Characters = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Objective = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Conditions = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Reward = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Extra = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    GeneratedText = reader.IsDBNull(10) ? "" : reader.GetString(10),

                    CreatedAt =
                        DateTime.TryParse(
                            reader.GetString(11),
                            out var createdAt)
                            ? createdAt
                            : DateTime.Now,

                    UpdatedAt =
                        DateTime.TryParse(
                            reader.GetString(12),
                            out var updatedAt)
                            ? updatedAt
                            : DateTime.Now
                });
        }

        return events;
    }

    public int AddRpEvent(RpEvent rpEvent)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO RpEvents
            (
                Title,
                EventType,
                Location,
                Faction,
                Characters,
                Objective,
                Conditions,
                Reward,
                Extra,
                GeneratedText,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $title,
                $eventType,
                $location,
                $faction,
                $characters,
                $objective,
                $conditions,
                $reward,
                $extra,
                $generatedText,
                $createdAt,
                $updatedAt
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", rpEvent.Title);
        command.Parameters.AddWithValue("$eventType", rpEvent.EventType);
        command.Parameters.AddWithValue("$location", rpEvent.Location);
        command.Parameters.AddWithValue("$faction", rpEvent.Faction);
        command.Parameters.AddWithValue("$characters", rpEvent.Characters);
        command.Parameters.AddWithValue("$objective", rpEvent.Objective);
        command.Parameters.AddWithValue("$conditions", rpEvent.Conditions);
        command.Parameters.AddWithValue("$reward", rpEvent.Reward);
        command.Parameters.AddWithValue("$extra", rpEvent.Extra);
        command.Parameters.AddWithValue("$generatedText", rpEvent.GeneratedText);
        command.Parameters.AddWithValue(
            "$createdAt",
            rpEvent.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedAt",
            rpEvent.UpdatedAt.ToString("O"));

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    public void UpdateRpEvent(RpEvent rpEvent)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE RpEvents
            SET
                Title = $title,
                EventType = $eventType,
                Location = $location,
                Faction = $faction,
                Characters = $characters,
                Objective = $objective,
                Conditions = $conditions,
                Reward = $reward,
                Extra = $extra,
                GeneratedText = $generatedText,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", rpEvent.Id);
        command.Parameters.AddWithValue("$title", rpEvent.Title);
        command.Parameters.AddWithValue("$eventType", rpEvent.EventType);
        command.Parameters.AddWithValue("$location", rpEvent.Location);
        command.Parameters.AddWithValue("$faction", rpEvent.Faction);
        command.Parameters.AddWithValue("$characters", rpEvent.Characters);
        command.Parameters.AddWithValue("$objective", rpEvent.Objective);
        command.Parameters.AddWithValue("$conditions", rpEvent.Conditions);
        command.Parameters.AddWithValue("$reward", rpEvent.Reward);
        command.Parameters.AddWithValue("$extra", rpEvent.Extra);
        command.Parameters.AddWithValue(
            "$generatedText",
            rpEvent.GeneratedText);
        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.Now.ToString("O"));

        if(command.ExecuteNonQuery()!=1) throw new InvalidOperationException("Запись для обновления не найдена.");
    }

    public void DeleteRpEvent(int id)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            DELETE FROM RpEvents
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);

        command.ExecuteNonQuery();
    }
}
