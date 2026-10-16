using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public enum ArticleGraphType
{
    Character,
    Place,
    Faction,
    Quest,
    Item,
    Creature,
    Race,
    Spell,
    Religion,
    Other
}

public static class ArticleGraphTypeClassifier
{
    public static ArticleGraphType Classify(Article article)
    {
        var category = article.Category;

        if (ArticleSectionMatcher.IsReligion(article))
            return ArticleGraphType.Religion;
        if (Contains(category, "персонаж"))
            return ArticleGraphType.Character;
        if (ContainsAny(category, "локац", "город", "деревн", "руин"))
            return ArticleGraphType.Place;
        if (ContainsAny(category, "фракц", "гильди", "легион", "компаньон", "организац", "клан", "культ"))
            return ArticleGraphType.Faction;
        if (Contains(category, "квест"))
            return ArticleGraphType.Quest;
        if (ContainsAny(category, "артефакт", "оружи", "доспех", "кинжал", "предмет"))
            return ArticleGraphType.Item;
        if (ContainsAny(category, "дракон", "нежить", "существ", "животн"))
            return ArticleGraphType.Creature;
        if (Contains(category, "рас"))
            return ArticleGraphType.Race;
        if (ContainsAny(category, "заклинан", "магия", "крик"))
            return ArticleGraphType.Spell;

        return ArticleGraphType.Other;
    }

    private static bool Contains(string value, string part)
    {
        return value.Contains(part, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] parts)
    {
        return parts.Any(part => Contains(value, part));
    }
}
