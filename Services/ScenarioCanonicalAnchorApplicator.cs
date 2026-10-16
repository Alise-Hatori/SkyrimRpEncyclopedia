using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Copies only explicitly selected canonical reference material into an already
/// generated scenario. It deliberately does not alter factory fiction.
/// </summary>
public static class ScenarioCanonicalAnchorApplicator
{
    public static GeneratedScenario Apply(
        GeneratedScenario scenario,
        IEnumerable<ScenarioCanonicalAnchor> anchors)
    {
        var confirmed = scenario.CanonicalAnchors
            .Concat(anchors)
            .Where(anchor => anchor.Status == ScenarioCanonicalAnchorStatus.Selected &&
                             anchor.Origin == ScenarioFactOrigin.CanonicalKnowledge &&
                             !anchor.NeedsReview)
            .GroupBy(anchor => anchor.EntityId)
            .Select(group => group.First())
            .ToArray();

        return new GeneratedScenario
        {
            Title = scenario.Title,
            Seed = scenario.Seed,
            Options = scenario.Options,
            Template = scenario.Template,
            Hook = scenario.Hook,
            ConflictCause = scenario.ConflictCause,
            GenerationDiagnostic = scenario.GenerationDiagnostic,
            InvestigationBlueprintId = scenario.InvestigationBlueprintId,
            IntrigueBlueprintId = scenario.IntrigueBlueprintId,
            MissionBlueprintId = scenario.MissionBlueprintId,
            NegotiationBlueprintId = scenario.NegotiationBlueprintId,
            StartSceneId = scenario.StartSceneId,
            PublicBriefing = scenario.PublicBriefing,
            KnownFacts = scenario.KnownFacts,
            NarrativeFacts = scenario.NarrativeFacts,
            GmNotes = scenario.GmNotes,
            Hypotheses = scenario.Hypotheses,
            Coalitions = scenario.Coalitions,
            Characters = scenario.Characters,
            Clues = scenario.Clues,
            Scenes = scenario.Scenes,
            Branches = scenario.Branches,
            Endings = scenario.Endings,
            CanonicalAnchors = confirmed
        };
    }
}
