using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Deterministically proposes source-backed candidates for a template. It does
/// not modify a scenario and never derives motives, alliances or causality from
/// a technical Knowledge DB relation.
/// </summary>
public sealed class ScenarioCanonicalAnchorSelector
{
    private readonly IScenarioKnowledgeCatalog _catalog;

    public ScenarioCanonicalAnchorSelector(IScenarioKnowledgeCatalog catalog)
    {
        _catalog = catalog;
    }

    public static IReadOnlyList<ScenarioCanonicalSelectionRule> Rules { get; } =
    [
        new()
        {
            TemplateId = "investigation",
            AnchorRules =
            [
                new() { Purpose = "объект расследования", SemanticTypes = ["NPC", "Faction"], PreferredCategories = ["Персонажи (Skyrim)", "Фракции (Skyrim)"], Limit = 2 },
                new() { Purpose = "проверяемый предмет или запись", SemanticTypes = ["Item", "Book"], PreferredCategories = ["Предметы (Skyrim)", "Квестовые предметы (Skyrim)"], Limit = 1 }
            ]
        },
        new()
        {
            TemplateId = "intrigue",
            AnchorRules =
            [
                new() { Purpose = "сторона политического спора", SemanticTypes = ["Faction", "NPC"], PreferredCategories = ["Фракции (Skyrim)", "Персонажи (Skyrim)"], Limit = 2 },
                new() { Purpose = "место решения", SemanticTypes = ["Place"], PreferredCategories = ["Локации (Skyrim)"], Limit = 1 }
            ]
        },
        new()
        {
            TemplateId = "mission",
            AnchorRules =
            [
                new() { Purpose = "место или маршрут поручения", SemanticTypes = ["Place"], PreferredCategories = ["Локации (Skyrim)"], Limit = 2 },
                new() { Purpose = "каноническая отправная точка поручения", SemanticTypes = ["Quest", "Item"], PreferredCategories = ["Квесты (Skyrim)", "Квестовые предметы (Skyrim)"], Limit = 1 }
            ]
        },
        new()
        {
            TemplateId = "negotiation",
            AnchorRules =
            [
                new() { Purpose = "предмет торгового спора", SemanticTypes = ["Item", "Weapon", "Armor"], PreferredCategories = ["Предметы (Skyrim)", "Оружие (Skyrim)"], Limit = 2 },
                new() { Purpose = "место переговоров", SemanticTypes = ["Place"], PreferredCategories = ["Локации (Skyrim)"], Limit = 1 }
            ]
        },
        new()
        {
            TemplateId = "conflict",
            AnchorRules =
            [
                new() { Purpose = "личная сторона конфликта", SemanticTypes = ["NPC"], PreferredCategories = ["Персонажи (Skyrim)"], Limit = 2 },
                new() { Purpose = "общественная сторона или место", SemanticTypes = ["Faction", "Place"], PreferredCategories = ["Фракции (Skyrim)", "Локации (Skyrim)"], Limit = 1 }
            ]
        }
    ];

    public ScenarioCanonicalSelectionResult FindCandidates(string templateId)
        => Select(templateId, seed: null);

    /// <summary>
    /// Suggests one deterministic, compatible set of candidates. Suggestions
    /// remain unconfirmed until the GM explicitly selects and applies them.
    /// </summary>
    public ScenarioCanonicalSelectionResult SuggestCandidates(string templateId, int seed)
        => Select(templateId, seed);

    private ScenarioCanonicalSelectionResult Select(string templateId, int? seed)
    {
        var catalogStatus = _catalog.GetStatus();
        if (!catalogStatus.IsCompatible)
            return new ScenarioCanonicalSelectionResult { CatalogStatus = catalogStatus, Diagnostic = catalogStatus.Diagnostic };

        var rule = Rules.SingleOrDefault(candidate => candidate.TemplateId.Equals(templateId, StringComparison.Ordinal));
        if (rule is null)
        {
            return new ScenarioCanonicalSelectionResult
            {
                CatalogStatus = catalogStatus,
                Diagnostic = $"Для каркаса '{templateId}' пока нет правил подбора канонических опор."
            };
        }

        var usedIds = new HashSet<int>();
        var anchors = new List<ScenarioCanonicalAnchor>();
        foreach (var anchorRule in rule.AnchorRules)
        {
            var pool = FindForRule(anchorRule)
                .Where(candidate => !candidate.NeedsReview)
                .ToList();
            if (seed.HasValue)
                Shuffle(pool, StableSeed(seed.Value, templateId, anchorRule.Purpose));

            foreach (var candidate in pool.Where(candidate => usedIds.Add(candidate.EntityId)).Take(anchorRule.Limit))
                anchors.Add(ToAnchor(candidate, anchorRule.Purpose, seed.HasValue ? ScenarioCanonicalAnchorStatus.Proposed : ScenarioCanonicalAnchorStatus.Candidate));
        }

        return new ScenarioCanonicalSelectionResult
        {
            CatalogStatus = catalogStatus,
            Anchors = anchors,
            Diagnostic = anchors.Count == 0
                ? "Для этого каркаса нет подходящих проверенных канонических опор. Автономный сценарий остаётся доступен."
                : "Найдены кандидаты. Они не вставлены в сюжет и требуют осознанного выбора ведущего."
        };
    }

    private IEnumerable<ScenarioKnowledgeCandidate> FindForRule(ScenarioCanonicalAnchorRule rule)
    {
        var pool = new Dictionary<int, ScenarioKnowledgeCandidate>();
        foreach (var semanticType in rule.SemanticTypes)
        {
            foreach (var category in rule.PreferredCategories)
                AddCandidates(pool, _catalog.Search(new ScenarioKnowledgeSearchRequest { SemanticType = semanticType, Category = category, Limit = Math.Max(4, rule.Limit * 3) }).Candidates);
            AddCandidates(pool, _catalog.Search(new ScenarioKnowledgeSearchRequest { SemanticType = semanticType, Limit = Math.Max(4, rule.Limit * 3) }).Candidates);
        }

        return pool.Values
            .OrderBy(candidate => MatchesPreferredCategory(candidate, rule.PreferredCategories) ? 0 : 1)
            .ThenBy(candidate => candidate.Title, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.EntityId);
    }

    private static void AddCandidates(
        IDictionary<int, ScenarioKnowledgeCandidate> destination,
        IEnumerable<ScenarioKnowledgeCandidate> candidates)
    {
        foreach (var candidate in candidates)
            destination.TryAdd(candidate.EntityId, candidate);
    }

    private static bool MatchesPreferredCategory(
        ScenarioKnowledgeCandidate candidate,
        IReadOnlyList<string> preferredCategories) =>
        preferredCategories.Contains(candidate.PrimaryCategory, StringComparer.OrdinalIgnoreCase) ||
        candidate.Categories.Any(category => preferredCategories.Contains(category, StringComparer.OrdinalIgnoreCase));

    public static ScenarioCanonicalAnchor WithStatus(ScenarioCanonicalAnchor anchor, ScenarioCanonicalAnchorStatus status) =>
        new()
        {
            EntityId = anchor.EntityId,
            Title = anchor.Title,
            SemanticType = anchor.SemanticType,
            Categories = anchor.Categories,
            SourceUrl = anchor.SourceUrl,
            ContextExcerpt = anchor.ContextExcerpt,
            NeedsReview = anchor.NeedsReview,
            Confidence = anchor.Confidence,
            Purpose = anchor.Purpose,
            Status = status,
            Origin = anchor.Origin
        };

    private static ScenarioCanonicalAnchor ToAnchor(
        ScenarioKnowledgeCandidate candidate,
        string purpose,
        ScenarioCanonicalAnchorStatus status) =>
        new()
        {
            EntityId = candidate.EntityId,
            Title = candidate.Title,
            SemanticType = candidate.SemanticType,
            Categories = candidate.Categories,
            SourceUrl = candidate.SourceUrl,
            ContextExcerpt = candidate.ContextExcerpt,
            NeedsReview = candidate.NeedsReview,
            Confidence = candidate.Confidence,
            Purpose = purpose,
            Status = status,
            Origin = ScenarioFactOrigin.CanonicalKnowledge
        };

    private static void Shuffle<T>(IList<T> values, int seed)
    {
        var random = new Random(seed);
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static int StableSeed(int seed, string templateId, string purpose)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in templateId + "\u001f" + purpose)
                hash = hash * 31 + character;
            return seed * 397 ^ hash;
        }
    }
}
