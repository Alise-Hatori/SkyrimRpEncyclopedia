namespace SkyrimRpEncyclopedia.Models;

public sealed record OverlayRelatedArticle(Article Article, string RelationType);

public static class OverlayRelatedArticleResolver
{
    public static List<OverlayRelatedArticle> Resolve(
        Article article,
        IEnumerable<ArticleLink> links,
        IEnumerable<Article> articles)
    {
        var availableArticles = articles.ToList();
        var relatedArticles = new List<OverlayRelatedArticle>();

        foreach (var link in links)
        {
            bool sourceMatches =
                link.SourceArticleId == article.Id &&
                link.SourceIsOfficial == article.IsOfficial;

            int relatedId = sourceMatches
                ? link.TargetArticleId
                : link.SourceArticleId;

            bool relatedIsOfficial = sourceMatches
                ? link.TargetIsOfficial
                : link.SourceIsOfficial;

            var related = availableArticles.FirstOrDefault(candidate =>
                candidate.Id == relatedId &&
                candidate.IsOfficial == relatedIsOfficial);

            if (related is not null)
                relatedArticles.Add(new OverlayRelatedArticle(related, link.LinkType));
        }

        return relatedArticles;
    }
}
