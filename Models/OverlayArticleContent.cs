using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Models;

// A display-only projection of the article already selected by MainWindow.
// It deliberately does not load or own article data.
public sealed record OverlayArticleContent(
    string Title,
    string Category,
    string Text,
    string? SourceUrl)
{
    public static OverlayArticleContent FromArticle(Article? article)
    {
        if (article is null)
        {
            return new OverlayArticleContent(
                "Статья не выбрана",
                "Откройте статью в основном окне энциклопедии.",
                "Оверлей покажет её заголовок, категорию и текст.",
                null);
        }

        var text = !string.IsNullOrWhiteSpace(article.Content)
            ? article.Content
            : !string.IsNullOrWhiteSpace(article.Summary)
                ? article.Summary
                : "Текст статьи отсутствует.";

        var sourceUrl = MvpKnowledgeDatabaseService.TryGetSafeSourceUri(article.SourceUrl, out var sourceUri)
            ? sourceUri!.AbsoluteUri
            : null;

        return new OverlayArticleContent(
            string.IsNullOrWhiteSpace(article.Title) ? "Без названия" : article.Title,
            string.IsNullOrWhiteSpace(article.Category) ? "Категория не указана" : article.Category,
            text,
            sourceUrl);
    }
}
