namespace SkyrimRpEncyclopedia.Models;

public sealed class ScenarioGenerationOptions
{
    public string Genre { get; set; } = "Переговоры";
    public int ParticipantCount { get; set; } = 3;
    public int DurationMinutes { get; set; } = 90;
    public string Tone { get; set; } = "Напряжённый";
    public string Difficulty { get; set; } = "Средняя";
    public int Seed { get; set; }
    public bool AllowMandatoryCombat { get; set; }
}

public sealed class ScenarioTemplate
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Genre { get; init; } = "";
    public int MinParticipants { get; init; }
    public int MaxParticipants { get; init; }
    public int MinDurationMinutes { get; init; }
    public int MaxDurationMinutes { get; init; }
    public bool RequiresCombat { get; init; }
    public string ConflictCause { get; init; } = "";
    public string Hook { get; init; } = "";
}

public sealed class ScenarioCharacter
{
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string Goal { get; init; } = "";
    public string Motive { get; init; } = "";
    public string PublicInformation { get; init; } = "";
    public string PersonalInformation { get; init; } = "";
    public string Concession { get; init; } = "";
    public string RedLine { get; init; } = "";
    public string Secret { get; init; } = "";
    public bool IsKeySecretHolder { get; init; }
}

public sealed class ScenarioClue
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public string KnownFact { get; init; } = "";
    public string Reveals { get; init; } = "";
    public IReadOnlyList<string> SupportsHypothesisIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ContradictsHypothesisIds { get; init; } = Array.Empty<string>();
}

public sealed class ScenarioHypothesis
{
    public string Id { get; init; } = "";
    public string Statement { get; init; } = "";
    public string Beneficiary { get; init; } = "";
    public string Cost { get; init; } = "";
    public string Unknown { get; init; } = "";
}

public sealed class ScenarioCoalition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Members { get; init; } = Array.Empty<string>();
    public string Concession { get; init; } = "";
    public string Cost { get; init; } = "";
    public IReadOnlyList<string> RequiredClueIds { get; init; } = Array.Empty<string>();
}

public sealed class ScenarioScene
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Purpose { get; init; } = "";
    public IReadOnlyList<string> ClueIds { get; init; } = Array.Empty<string>();
}

public sealed class ScenarioBranch
{
    public string FromSceneId { get; init; } = "";
    public string ToSceneId { get; init; } = "";
    public string ToEndingId { get; init; } = "";
    public string Decision { get; init; } = "";
    public string Condition { get; init; } = "";
    public IReadOnlyList<string> RequiredClueIds { get; init; } = Array.Empty<string>();
}

public sealed class ScenarioEnding
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Outcome { get; init; } = "";
    public bool IsReachable { get; init; }
    public bool RequiresCombat { get; init; }
}

/// <summary>
/// A short, explicitly classified statement used by a blueprint. Canonical
/// facts must name their source record; factory fiction and GM hypotheses are
/// never promoted to lore merely because a scenario has reference anchors.
/// </summary>
public sealed class ScenarioNarrativeFact
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public ScenarioFactOrigin Origin { get; init; } = ScenarioFactOrigin.FactoryFiction;
    public int? SourceEntityId { get; init; }
    public string SourceUrl { get; init; } = "";
}

public sealed class GeneratedScenario
{
    public string Title { get; init; } = "";
    public int Seed { get; init; }
    public ScenarioGenerationOptions Options { get; init; } = new();
    public ScenarioTemplate Template { get; init; } = new();
    public string Hook { get; init; } = "";
    public string ConflictCause { get; init; } = "";
    public string GenerationDiagnostic { get; init; } = "";
    /// <summary>Empty for the legacy factories; populated by the investigation pilot.</summary>
    public string InvestigationBlueprintId { get; init; } = "";
    /// <summary>Empty for the legacy factories; populated by the intrigue pilot.</summary>
    public string IntrigueBlueprintId { get; init; } = "";
    /// <summary>Empty for previously exported mission scenarios.</summary>
    public string MissionBlueprintId { get; init; } = "";
    public string NegotiationBlueprintId { get; init; } = "";
    public string StartSceneId { get; init; } = "opening";
    public string PublicBriefing { get; init; } = "";
    public IReadOnlyList<string> KnownFacts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ScenarioNarrativeFact> NarrativeFacts { get; init; } = Array.Empty<ScenarioNarrativeFact>();
    public IReadOnlyList<string> GmNotes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ScenarioHypothesis> Hypotheses { get; init; } = Array.Empty<ScenarioHypothesis>();
    public IReadOnlyList<ScenarioCoalition> Coalitions { get; init; } = Array.Empty<ScenarioCoalition>();
    public IReadOnlyList<ScenarioCharacter> Characters { get; init; } = Array.Empty<ScenarioCharacter>();
    public IReadOnlyList<ScenarioClue> Clues { get; init; } = Array.Empty<ScenarioClue>();
    public IReadOnlyList<ScenarioScene> Scenes { get; init; } = Array.Empty<ScenarioScene>();
    public IReadOnlyList<ScenarioBranch> Branches { get; init; } = Array.Empty<ScenarioBranch>();
    public IReadOnlyList<ScenarioEnding> Endings { get; init; } = Array.Empty<ScenarioEnding>();
    /// <summary>
    /// Explicitly confirmed, source-backed reference material. It is separate
    /// from factory-fiction scenes, roles, clues, branches and endings.
    /// </summary>
    public IReadOnlyList<ScenarioCanonicalAnchor> CanonicalAnchors { get; init; } = Array.Empty<ScenarioCanonicalAnchor>();
}

public sealed class ScenarioValidationResult
{
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool IsValid => Errors.Count == 0;
}

public sealed class ScenarioGenerationResult
{
    public GeneratedScenario? Scenario { get; init; }
    public ScenarioValidationResult Validation { get; init; } = new();
    public string Diagnostic { get; init; } = "";
}
