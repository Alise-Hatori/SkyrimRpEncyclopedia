using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public sealed record ArticleFilterValue(ArticleCatalogSource Source, string Value)
{
    public string DisplayName => $"{Value} · {Source switch { ArticleCatalogSource.Rules => "RP-правила", ArticleCatalogSource.Knowledge => "Knowledge", ArticleCatalogSource.User => "Личные", _ => "Официальные" }}";
}
public sealed record ArticleFilterRequest(ArticleCatalogSource? Source, ArticleFilterValue? Category,
    IReadOnlyList<ArticleFilterValue> Tags, IReadOnlyList<ArticleFilterValue> Flags);
public static class ArticleFilterRules
{
    public static ArticleCatalogSource SourceOf(Article a) => a.IsRpRule ? ArticleCatalogSource.Rules : a.IsMvp ? ArticleCatalogSource.Knowledge : a.IsOfficial ? ArticleCatalogSource.Official : ArticleCatalogSource.User;
    public static string[] Tokens(string value) => value.Split([',',';','\r','\n'], StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static IReadOnlyList<ArticleFilterValue> Options(IEnumerable<Article> articles, Func<Article,IEnumerable<string>> values) =>
        articles.SelectMany(a=>values(a).Where(v=>!string.IsNullOrWhiteSpace(v)).Select(v=>new ArticleFilterValue(SourceOf(a),v.Trim())))
            .GroupBy(v=>(v.Source,v.Value.ToUpperInvariant())).Select(g=>g.First()).OrderBy(v=>v.Value,StringComparer.OrdinalIgnoreCase).ThenBy(v=>v.Source).ToArray();
    public static IEnumerable<string> Categories(Article a) => new[]{a.Category}.Concat(a.AdditionalCategories);
    public static bool Matches(Article a, ArticleFilterRequest request)
    {
        var source=SourceOf(a);
        bool Includes(IEnumerable<string> values,ArticleFilterValue option) => source==option.Source && values.Contains(option.Value,StringComparer.OrdinalIgnoreCase);
        return (request.Source is null || source==request.Source) &&
            (request.Category is null || Includes(Categories(a),request.Category)) &&
            request.Tags.All(t=>Includes(Tokens(a.Tags),t)) && request.Flags.All(f=>Includes(Tokens(a.Flags),f));
    }
}
