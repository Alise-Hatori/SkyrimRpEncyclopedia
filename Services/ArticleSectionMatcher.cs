using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public static class ArticleSectionMatcher
{
    private static readonly HashSet<string> ReligionCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Божества", "Девять Божеств", "Князья даэдра"
    };

    public static bool Matches(string section, Article article)
    {
        if (article.Category.Equals(section, StringComparison.OrdinalIgnoreCase))
            return true;

        return section switch
        {
            "Лор Skyrim" => HasCategory(article, "Lore"),
            "Боги и даэдра" => article.AdditionalCategories.Any(ReligionCategories.Contains),
            _ => false
        };
    }

    public static bool IsReligion(Article article)
    {
        return article.Category.Equals("Боги и даэдра", StringComparison.OrdinalIgnoreCase) ||
               article.AdditionalCategories.Any(ReligionCategories.Contains);
    }

    private static bool HasCategory(Article article, string category) =>
        article.Category.Equals(category, StringComparison.OrdinalIgnoreCase) ||
        article.AdditionalCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
}
