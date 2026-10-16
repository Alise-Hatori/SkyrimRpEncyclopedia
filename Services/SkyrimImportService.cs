using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public class SkyrimImportService
{
    private readonly DatabaseService _database;

    public SkyrimImportService(
        DatabaseService database)
    {
        _database = database;
    }

    public ImportResult ImportArticle(
        ImportedArticle imported)
    {
        if (string.IsNullOrWhiteSpace(imported.Title))
        {
            throw new ArgumentException(
                "У импортируемой статьи отсутствует название.");
        }

        if (string.IsNullOrWhiteSpace(imported.Category))
        {
            throw new ArgumentException(
                $"У статьи '{imported.Title}' отсутствует категория.");
        }

        string title =
            imported.Title.Trim();

        string category =
            imported.Category.Trim();

        string summary =
            imported.Summary?.Trim() ?? "";

        string content =
            imported.Content?.Trim() ?? "";

        _database.GetOrCreateCategory(
            category);

        var cleanTags =
            imported.Tags
                .Where(
                    x => !string.IsNullOrWhiteSpace(x))
                .Select(
                    x => x.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        string tags =
            string.Join(
                ", ",
                cleanTags);

        var existing =
            _database.FindArticleByTitle(
                title);

        int articleId;

        if (existing == null)
        {
            var article =
                new Article
                {
                    Title =
                        title,

                    Category =
                        category,

                    Summary =
                        summary,

                    Content =
                        content,

                    Tags =
                        tags,

                    Flags =
                        imported.Flags?.Trim() ?? "",

                    IsFavorite =
                        false,

                    IsOfficial =
                        imported.IsOfficial,

                    CreatedAt =
                        DateTime.Now,

                    UpdatedAt =
                        DateTime.Now
                };

            articleId =
                _database.AddArticle(
                    article);
        }
        else
        {
            existing.Category =
                category;

            existing.Summary =
                summary;

            existing.Content =
                content;

            existing.Tags =
                tags;

            existing.Flags =
                imported.Flags?.Trim() ?? "";

            existing.IsOfficial =
                imported.IsOfficial;

            existing.UpdatedAt =
                DateTime.Now;

            _database.UpdateArticle(
                existing);

            articleId =
                existing.Id;
        }

        _database.SetArticleTags(
            articleId,
            cleanTags);

        if (!string.IsNullOrWhiteSpace(
                imported.SourceUrl))
        {
            AddOrUpdateSource(
                articleId,
                imported);
        }

        return new ImportResult
        {
            ArticleId =
                articleId,

            Updated =
                existing != null,

            Title =
                title
        };
    }

    public async Task<ImportResult?> ImportPage(
        string url,
        string category)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException(
                "URL страницы не указан.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException(
                "Категория статьи не указана.");
        }

        var webImporter =
            new SkyrimWebImportService();

        var article =
            await webImporter.ImportPage(
                url,
                category);

        if (article == null)
            return null;

        return ImportArticle(
            article);
    }

    public ImportBatchResult ImportBatch(
        IEnumerable<ImportedArticle> articles)
    {
        var result =
            new ImportBatchResult();

        var items =
            articles.ToList();

        result.Total =
            items.Count;

        foreach (var imported in items)
        {
            try
            {
                var importResult =
                    ImportArticle(
                        imported);

                if (importResult.Updated)
                {
                    result.Updated++;
                }
                else
                {
                    result.Added++;
                }
            }
            catch (Exception ex)
            {
                result.Failed++;

                result.Errors.Add(
                    $"{imported.Title}: {ex.Message}");
            }
        }

        return result;
    }

    private void AddOrUpdateSource(
        int articleId,
        ImportedArticle imported)
    {
        var sources =
            _database.GetArticleSources(
                articleId,
                imported.IsOfficial);

        string sourceUrl =
            imported.SourceUrl.Trim();

        var existing =
            sources.FirstOrDefault(
                x =>
                    string.Equals(
                        x.Url,
                        sourceUrl,
                        StringComparison.OrdinalIgnoreCase));

        string sourceTitle =
            string.IsNullOrWhiteSpace(
                imported.SourceTitle)
                ? imported.Title.Trim()
                : imported.SourceTitle.Trim();

        string sourceType =
            string.IsNullOrWhiteSpace(
                imported.SourceType)
                ? "Website"
                : imported.SourceType.Trim();

        if (existing == null)
        {
            var source =
                new ArticleSource
                {
                    ArticleId =
                        articleId,

                    ArticleIsOfficial =
                        imported.IsOfficial,

                    Title =
                        sourceTitle,

                    Url =
                        sourceUrl,

                    SourceType =
                        sourceType,

                    Author =
                        "",

                    Description =
                        "",

                    CreatedAt =
                        DateTime.Now,

                    UpdatedAt =
                        DateTime.Now
                };

            _database.AddSource(
                source);

            return;
        }

        existing.Title =
            sourceTitle;

        existing.SourceType =
            sourceType;

        existing.UpdatedAt =
            DateTime.Now;

        _database.UpdateSource(
            existing);
    }
}

public class ImportResult
{
    public int ArticleId { get; set; }

    public bool Updated { get; set; }

    public string Title { get; set; } = "";
}