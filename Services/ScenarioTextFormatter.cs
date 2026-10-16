using System.Text;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Produces a read-only, copy-friendly view of a generated scenario without
/// changing any of its factory data.
/// </summary>
public static class ScenarioTextFormatter
{
    public static string GetDisplayName(GeneratedScenario scenario) =>
        scenario.Template.Id == "negotiation" && !string.IsNullOrWhiteSpace(scenario.NegotiationBlueprintId)
            ? scenario.Title
            : scenario.Template.Name;
    public static string FormatScenario(GeneratedScenario scenario)
    {
        var text = new StringBuilder();
        text.AppendLine(scenario.Title);
        text.AppendLine($"Каркас: {GetDisplayName(scenario)}. Seed: {scenario.Seed}.");
        text.AppendLine();
        text.AppendLine("Завязка: " + scenario.Hook);
        text.AppendLine("Публично известно: " + scenario.PublicBriefing);
        text.AppendLine("Причина конфликта: " + scenario.ConflictCause);
        if (!string.IsNullOrWhiteSpace(scenario.InvestigationBlueprintId))
            text.AppendLine("Blueprint расследования: " + scenario.InvestigationBlueprintId);
        if (!string.IsNullOrWhiteSpace(scenario.IntrigueBlueprintId))
            text.AppendLine("Blueprint интриги: " + scenario.IntrigueBlueprintId);
        if (!string.IsNullOrWhiteSpace(scenario.MissionBlueprintId))
            text.AppendLine("Blueprint миссии: " + scenario.MissionBlueprintId);
        AppendSection(text, "Исходные факты", scenario.KnownFacts);
        if (!string.IsNullOrWhiteSpace(scenario.NegotiationBlueprintId))
            text.AppendLine("Blueprint переговоров: " + scenario.NegotiationBlueprintId);
        AppendSection(text, "Происхождение фактов", scenario.NarrativeFacts.Select(FormatNarrativeFact));
        AppendSection(text, "Канонические справочные опоры", scenario.CanonicalAnchors.Select(anchor =>
            $"{anchor.Title} — {anchor.SemanticType}\nСтатус: {AnchorStatusText(anchor.Status)}\nНазначение: {anchor.Purpose}\nИсточник: {anchor.SourceUrl}\n{anchor.ContextExcerpt}\nЭто справочный материал, а не обязательный сюжетный факт."));
        AppendSection(text, "Версии расследования", scenario.Hypotheses.Select(hypothesis =>
            $"{hypothesis.Statement}\nКому выгодно: {hypothesis.Beneficiary}; цена: {hypothesis.Cost}\nНеизвестно: {hypothesis.Unknown}"));
        AppendSection(text, "Коалиции и уступки", scenario.Coalitions.Select(coalition =>
            $"{coalition.Name}\nУчастники: {string.Join(", ", coalition.Members)}\nУступка: {coalition.Concession}\nЦена: {coalition.Cost}\n{FormatClueRequirements("Обязательные улики", coalition.RequiredClueIds, scenario.Clues)}"));
        AppendSection(text, "Персональные карточки ролей", scenario.Characters.Select(role =>
            $"{role.Name} — {role.Role}\nЦель: {role.Goal}\nМотив: {role.Motive}\nПублично: {role.PublicInformation}\nЛично: {role.PersonalInformation}\nГотов принять: {role.Concession}\nКрасная линия: {role.RedLine}"));
        AppendSection(text, "Улики", scenario.Clues.Select(clue =>
            $"{clue.Text}\nФакт: {clue.KnownFact}; раскрывает: {clue.Reveals}"));
        AppendSection(text, "Сцены", scenario.Scenes.Select(scene =>
            $"{scene.Title}: {scene.Purpose}\n{FormatClueRequirements("Связанные улики", scene.ClueIds, scenario.Clues)}"));
        AppendSection(text, "Ветвления", scenario.Branches.Select(branch =>
            $"{branch.FromSceneId} → {(string.IsNullOrWhiteSpace(branch.ToEndingId) ? branch.ToSceneId : "финал: " + branch.ToEndingId)}\nДействие: {branch.Decision}\nУсловие: {branch.Condition}{(branch.RequiredClueIds.Count == 0 ? "" : "\nНужные улики: " + string.Join(", ", branch.RequiredClueIds))}"));
        AppendSection(text, "Финалы", scenario.Endings.Select(ending =>
            $"{ending.Title}: {ending.Outcome}\nУсловия достижения: {FormatEndingConditions(ending, scenario)}"));
        AppendSection(text, "Только для ведущего", scenario.GmNotes.Concat(scenario.Characters
            .Where(role => !string.IsNullOrWhiteSpace(role.Secret))
            .Select(role => $"{role.Name}: {role.Secret}")));
        return text.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder text, string title, IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (items.Length == 0)
            return;

        text.AppendLine();
        text.AppendLine(title + ":");
        foreach (var item in items)
        {
            text.AppendLine(item);
            text.AppendLine();
        }
    }

    private static string FormatNarrativeFact(ScenarioNarrativeFact fact)
    {
        var source = fact.Origin == ScenarioFactOrigin.CanonicalKnowledge && !string.IsNullOrWhiteSpace(fact.SourceUrl)
            ? $"\nИсточник: {fact.SourceUrl}"
            : "";
        return $"{FactOriginText(fact.Origin)}: {fact.Text}{source}";
    }

    private static string FormatClueRequirements(string label, IReadOnlyList<string> clueIds, IReadOnlyList<ScenarioClue> clues)
    {
        if (clueIds.Count == 0)
            return label + ": не заданы.";

        var byId = clues.ToDictionary(clue => clue.Id, StringComparer.Ordinal);
        return label + ": " + string.Join("; ", clueIds.Select(id =>
            byId.TryGetValue(id, out var clue) ? $"{id} — {clue.Text}" : id));
    }

    private static string FormatEndingConditions(ScenarioEnding ending, GeneratedScenario scenario)
    {
        var branches = scenario.Branches.Where(branch => branch.ToEndingId == ending.Id).ToArray();
        if (branches.Length == 0)
            return "не заданы в переходах.";

        return string.Join("; ", branches.Select(branch =>
            $"из сцены {branch.FromSceneId}: {branch.Condition}" +
            (branch.RequiredClueIds.Count == 0 ? "" : "; нужны улики: " + string.Join(", ", branch.RequiredClueIds))));
    }

    private static string FactOriginText(ScenarioFactOrigin origin) => origin switch
    {
        ScenarioFactOrigin.CanonicalKnowledge => "Подтверждённый канон",
        ScenarioFactOrigin.GmHypothesis => "Гипотеза ведущего, не подтверждённая истина",
        _ => "Вымышленный исходный факт сценария"
    };

    private static string AnchorStatusText(ScenarioCanonicalAnchorStatus status) => status switch
    {
        ScenarioCanonicalAnchorStatus.Proposed => "Предложено по seed",
        ScenarioCanonicalAnchorStatus.Selected => "Подтверждено ведущим",
        ScenarioCanonicalAnchorStatus.Rejected => "Отклонено ведущим",
        _ => "Кандидат"
    };
}
