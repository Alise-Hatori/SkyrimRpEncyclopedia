using System.IO;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public static class RpRulesCatalog
{
    public static string ResolvePath(string? applicationDirectory = null) =>
        Path.Combine(applicationDirectory ?? AppContext.BaseDirectory, "Data", "rp_rules.db");

    public static int ToArticleId(int originalId) => originalId > 0 ? -originalId
        : throw new InvalidDataException("ID пакетной RP-статьи должен быть положительным.");

    // Prefer editable personal copies only when all public fields match. Never deduplicate by ID/title alone.
    public static List<Article> PreferPersonalCopies(IEnumerable<Article> articles)
    {
        var rows = articles.ToList();
        var personal = rows.Where(a => !a.IsOfficial).ToLookup(a => (a.Title, a.Category));
        return rows.Where(a => !a.IsRpRule || !personal[(a.Title, a.Category)].Any(p => Equivalent(p,a))).ToList();
    }

    public static bool Equivalent(Article personal, Article rule) => personal.Title == rule.Title && personal.Category == rule.Category &&
        personal.Summary == rule.Summary && personal.Tags == rule.Tags && personal.Flags == rule.Flags && personal.Content == rule.Content;
}
